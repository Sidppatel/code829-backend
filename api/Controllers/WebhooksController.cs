using System.Text.Json;
using Api.Services;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using Stripe;

namespace Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    ISecretsProvider secrets,
    IStripeTransactionProcedures stripeTransactionProc,
    IPurchaseProcedures purchaseProc,
    IOrganizationProcedures organizationProc,
    IStripeEventProcedures stripeEventProc,
    IConnectionMultiplexer redis,
    IAlertService alertService,
    IPaymentEnrichmentService paymentEnrichment
) : ControllerBase
{
    private static readonly TimeSpan DedupeTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan InflightTtl = TimeSpan.FromSeconds(60);
    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        Event stripeEvent;
        try
        {
            var webhookSecret = secrets.StripeWebhookSecret;
            if (string.IsNullOrEmpty(webhookSecret))
            {
                Log.Error("[Webhook] stripe_webhook_secret not configured — rejecting request");
                return StatusCode(500, "Webhook secret not configured");
            }

            var signature = Request.Headers["Stripe-Signature"].ToString();
            // Stripe CLI forwards events using the newest API version while the SDK is pinned to
            // an older one. Signature + payload are still validated; we only read core fields
            // (PaymentIntent.Id, AmountReceived, Metadata) that are stable across versions.
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            Log.Warning(ex, "[Webhook] Invalid Stripe signature");
            return BadRequest("Invalid signature");
        }

        // Dedupe at the controller level — Stripe retries failed webhooks and a single event
        // can arrive multiple times. Idempotent handlers guard downstream work, but processing
        // the same event twice still burns DB + Stripe API calls.
        var dedupeKey = $"stripe-webhook:{stripeEvent.Id}";
        var inflightKey = $"stripe-webhook:inflight:{stripeEvent.Id}";
        var db = redis.GetDatabase();
        var firstSeen = await db.StringSetAsync(dedupeKey, "1", DedupeTtl, When.NotExists);
        if (!firstSeen)
        {
            Log.Information("[Webhook] Duplicate event {EventId} ({EventType}) — skipping", stripeEvent.Id, stripeEvent.Type);
            return Ok();
        }

        // Short-lived in-flight lock guards against two workers racing the same event id.
        // The 7-day dedupe is cleared on handler failure (below) to allow Stripe retries;
        // without the 60s inflight lock, concurrent retries after a crash could double-process.
        var gotInflight = await db.StringSetAsync(inflightKey, "1", InflightTtl, When.NotExists);
        if (!gotInflight)
        {
            Log.Warning("[Webhook] Event {EventId} already in-flight — returning 409 so Stripe retries", stripeEvent.Id);
            // Release the dedupe key we just set so a fresh retry can enter after the
            // current in-flight attempt either succeeds (and the dedupe stays) or crashes
            // (which also deletes the dedupe in its catch block).
            await db.KeyDeleteAsync(dedupeKey);
            return StatusCode(409);
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case EventTypes.PaymentIntentSucceeded:
                    await HandlePaymentIntentSucceeded(stripeEvent);
                    break;
                case EventTypes.PaymentIntentPaymentFailed:
                    await HandlePaymentIntentFailed(stripeEvent);
                    break;
                case EventTypes.ChargeRefundUpdated:
                    await HandleRefundUpdated(stripeEvent);
                    break;
                case EventTypes.AccountUpdated:
                    await HandleAccountUpdated(stripeEvent);
                    break;
                case EventTypes.TransferCreated:
                    await HandleTransferCreated(stripeEvent);
                    break;
                case EventTypes.PayoutCreated:
                    await HandlePayoutCreated(stripeEvent);
                    break;
                case EventTypes.PayoutPaid:
                    await HandlePayoutPaid(stripeEvent);
                    break;
                default:
                    Log.Information("[Webhook] Unhandled event type: {Type}", stripeEvent.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Webhook] Error processing {EventType} {EventId}", stripeEvent.Type, stripeEvent.Id);
            // Clear the dedupe key so Stripe's retry has a chance
            await db.KeyDeleteAsync(dedupeKey);
        }
        finally
        {
            await db.KeyDeleteAsync(inflightKey);
        }

        return Ok();
    }

    private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null) return;

        var txn = await stripeTransactionProc.GetByPaymentIntentAsync(paymentIntent.Id);

        if (txn is null)
        {
            Log.Warning("[Webhook] No stripe transaction found for intent {IntentId}", paymentIntent.Id);
            return;
        }

        if (txn.Status == PaymentStatus.Succeeded)
        {
            Log.Information("[Webhook] Transaction {IntentId} already confirmed (idempotent skip)", paymentIntent.Id);
            return;
        }

        var expectedAmount = txn.AmountCents;
        if ((int)paymentIntent.AmountReceived != expectedAmount)
        {
            Log.Error(
                "[Webhook] PAYMENT_AMOUNT_MISMATCH intent={IntentId} purchase={PurchaseId} expected={Expected} received={Received}",
                paymentIntent.Id, txn.PurchaseId, expectedAmount, paymentIntent.AmountReceived);
            await alertService.RaiseAsync(
                "PAYMENT_AMOUNT_MISMATCH",
                "Stripe payment_intent.succeeded amount did not match expected; purchase auto-cancelled.",
                new Dictionary<string, string>
                {
                    ["purchaseId"] = txn.PurchaseId.ToString(),
                    ["paymentIntentId"] = paymentIntent.Id,
                    ["expectedCents"] = expectedAmount.ToString(),
                    ["receivedCents"] = paymentIntent.AmountReceived.ToString(),
                });
            await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Failed");
            await purchaseProc.CancelPurchaseAsync(txn.PurchaseId);
            return;
        }

        await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Succeeded");
        await purchaseProc.ConfirmPurchaseAsync(txn.PurchaseId, "");
        Log.Information("[Webhook] Payment confirmed for purchase {PurchaseId}", txn.PurchaseId);

        // Enrich + record tax — shared with PurchaseService.ConfirmAsync so the
        // FE-driven confirm path also lands the tax data in dev (no public
        // webhook URL there). See IPaymentEnrichmentService for idempotency.
        await paymentEnrichment.EnrichAndRecordAsync(paymentIntent.Id, txn.TaxCalculationId);
    }

    private async Task HandlePaymentIntentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null) return;

        var txn = await stripeTransactionProc.GetByPaymentIntentAsync(paymentIntent.Id);

        if (txn is null) return;

        if (txn.Status == PaymentStatus.Failed)
            return;

        await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Failed");
        await purchaseProc.CancelPurchaseAsync(txn.PurchaseId);

        Log.Warning("[Webhook] Payment failed for purchase {PurchaseId}: {Reason}",
            txn.PurchaseId, paymentIntent.LastPaymentError?.Message ?? "unknown");
    }

    private async Task HandleRefundUpdated(Event stripeEvent)
    {
        var refund = stripeEvent.Data.Object as Refund;
        if (refund?.PaymentIntentId is null) return;

        var txn = await stripeTransactionProc.GetByPaymentIntentAsync(refund.PaymentIntentId);

        if (txn is null) return;

        if (refund.Status == "succeeded" && txn.Status != PaymentStatus.Refunded)
        {
            await stripeTransactionProc.UpdateStatusAsync(refund.PaymentIntentId, "Refunded");
            await purchaseProc.RefundPurchaseAsync(txn.PurchaseId);
            Log.Information("[Webhook] Refund synced for purchase {PurchaseId}", txn.PurchaseId);
        }
    }

    // ───────────────────────────────────────────────────────────────────
    // Connect events: account.updated, transfer.created, payout.{created,paid}
    //
    // The platform receives these on the same /webhooks/stripe endpoint as the
    // PaymentIntent flow, distinguished by event type. The Redis dedupe + inflight
    // lock above protects every event id equally — no Connect-specific guard
    // needed. Each handler is idempotent: account.updated UPDATEs; transfer.created
    // INSERT ... ON CONFLICT DO NOTHING; payout.* upserts.
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mirror Stripe's account.charges_enabled / payouts_enabled / details_submitted
    /// flags onto the corresponding Organization row. Also persists the
    /// requirements.currently_due array so the admin dashboard can show "still
    /// need: tax_id, ssn_last_4" without round-tripping Stripe on every render.
    ///
    /// First-time-completed onboarding (DetailsSubmitted flips to true) sets
    /// Organization.StripeOnboardedAt — the SP guards the timestamp so subsequent
    /// account.updated events can never overwrite it.
    /// </summary>
    private async Task HandleAccountUpdated(Event stripeEvent)
    {
        var account = stripeEvent.Data.Object as Account;
        if (account is null)
        {
            Log.Warning("[Webhook] account.updated payload not parseable as Account");
            return;
        }

        var requirementsJson = JsonSerializer.Serialize(
            account.Requirements?.CurrentlyDue?.ToList() ?? new List<string>());

        try
        {
            await organizationProc.UpdateStripeStatusAsync(
                account.Id,
                account.ChargesEnabled, account.PayoutsEnabled, account.DetailsSubmitted,
                requirementsJson);

            Log.Information(
                "[Webhook] account.updated processed for {AccountId}: charges={Charges} payouts={Payouts} details={Details}",
                account.Id, account.ChargesEnabled, account.PayoutsEnabled, account.DetailsSubmitted);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0002" /* no_data_found from RAISE */ )
        {
            // The org wasn't found — likely an account created outside our system, or a
            // race where the org row hasn't landed yet. Log + swallow rather than 500;
            // letting Stripe retry in this case won't help.
            Log.Warning(
                "[Webhook] account.updated for unknown account {AccountId} — no organization linked",
                account.Id);
        }
    }

    /// <summary>
    /// Append-only audit of platform → connected-account transfers. We resolve
    /// the originating Purchase via the source charge's PaymentIntent (best-effort —
    /// the row is still useful for the org dashboard even if PurchaseId is null).
    /// </summary>
    private async Task HandleTransferCreated(Event stripeEvent)
    {
        var transfer = stripeEvent.Data.Object as Transfer;
        if (transfer is null)
        {
            Log.Warning("[Webhook] transfer.created payload not parseable as Transfer");
            return;
        }

        if (string.IsNullOrEmpty(transfer.DestinationId))
        {
            Log.Warning("[Webhook] transfer.created {TransferId} has no destination — skipping", transfer.Id);
            return;
        }

        // Resolve the platform PaymentIntent from the source charge. Stripe.net
        // exposes SourceTransactionId as the charge id; we expand only when we
        // have a stripe key. Best-effort: a transfer without a resolvable PI is
        // still recorded with PurchaseId=null.
        string? paymentIntentId = null;
        PaymentIntent? sourcePi = null;
        if (!string.IsNullOrEmpty(transfer.SourceTransactionId) && !string.IsNullOrEmpty(secrets.StripeSecretKey))
        {
            try
            {
                var client = new StripeClient(secrets.StripeSecretKey);
                var charge = await new ChargeService(client).GetAsync(transfer.SourceTransactionId);
                paymentIntentId = charge.PaymentIntentId;
                if (!string.IsNullOrEmpty(paymentIntentId))
                    sourcePi = await new PaymentIntentService(client).GetAsync(paymentIntentId);
            }
            catch (StripeException ex)
            {
                Log.Warning(ex, "[Webhook] transfer.created {TransferId} — failed to resolve source charge {ChargeId}",
                    transfer.Id, transfer.SourceTransactionId);
            }
        }

        // Enrich the destination CHARGE (the one visible in the organizer's
        // Stripe Express dashboard) AND the platform-side Transfer. Destination
        // charges create a separate charge under the connected account whose
        // metadata + description default to empty, so the Express dashboard
        // shows the generic "Payment from <platform>" string. Updating both
        // the Charge (under the Stripe-Account header) and the Transfer (on
        // the platform) gives the organizer the booking context everywhere.
        if (sourcePi is not null && !string.IsNullOrEmpty(secrets.StripeSecretKey))
        {
            // Older bookings predate the PI.description code path — fall back
            // to building the same string from PI metadata so historical
            // events resent from the Stripe dashboard still get a useful label.
            var description = !string.IsNullOrEmpty(sourcePi.Description)
                ? sourcePi.Description
                : Services.PaymentDescriptions.FromMetadata(sourcePi.Metadata);

            if (!string.IsNullOrEmpty(description))
            {
                var client = new StripeClient(secrets.StripeSecretKey);
                var metadata = sourcePi.Metadata is { Count: > 0 }
                    ? new Dictionary<string, string>(sourcePi.Metadata)
                    : null;

                // Platform-side Transfer.update — surfaces the booking context
                // in the platform's Stripe events feed and any future audit
                // view that reads stripe_transfers.RawEvent.
                try
                {
                    await new TransferService(client).UpdateAsync(transfer.Id, new TransferUpdateOptions
                    {
                        Description = description,
                        Metadata = metadata
                    });
                    Log.Information(
                        "[Webhook] transfer.created updated platform transfer {TransferId} description",
                        transfer.Id);
                }
                catch (StripeException ex)
                {
                    Log.Warning(ex,
                        "[Webhook] transfer.created — failed to update platform transfer {TransferId} description",
                        transfer.Id);
                    // Non-fatal: the destination charge update below is the
                    // user-visible fix. Don't abort the whole handler if
                    // Stripe rejects the platform-side update (e.g. on an
                    // already-reversed transfer).
                }

                // Destination charge update — drives the organizer's Express
                // dashboard description column. Re-throws on failure so the
                // outer handler clears the Redis dedupe key and Stripe retries.
                // Charge.update is idempotent.
                if (!string.IsNullOrEmpty(transfer.DestinationPaymentId))
                {
                    var chargeOpts = new ChargeUpdateOptions
                    {
                        Description = description,
                        Metadata = metadata
                    };
                    var requestOpts = new RequestOptions { StripeAccount = transfer.DestinationId };
                    await new ChargeService(client).UpdateAsync(transfer.DestinationPaymentId, chargeOpts, requestOpts);
                    Log.Information(
                        "[Webhook] transfer.created enriched destination charge {ChargeId} on {Account} with desc + metadata",
                        transfer.DestinationPaymentId, transfer.DestinationId);
                }
            }
            else
            {
                Log.Warning(
                    "[Webhook] transfer.created {TransferId} — source PI {PiId} has no description and no usable metadata; skipping enrichment",
                    transfer.Id, paymentIntentId);
            }
        }

        // ToJson lives on StripeEntity — Data.Object is typed as IHasObject so we
        // cast through to get the SDK's serialized payload (omits undocumented
        // properties; that's fine for our forensic store).
        var rawJson = (stripeEvent.Data.Object as StripeEntity)?.ToJson() ?? "{}";

        try
        {
            var rowId = await stripeEventProc.InsertTransferAsync(
                transfer.Id, transfer.DestinationId, paymentIntentId,
                (int)transfer.Amount, transfer.Currency, rawJson);

            Log.Information(
                "[Webhook] transfer.created recorded {RowId} for {TransferId} ({Amount} {Currency} → {Destination})",
                rowId, transfer.Id, transfer.Amount, transfer.Currency, transfer.DestinationId);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0002")
        {
            Log.Warning(
                "[Webhook] transfer.created {TransferId} — destination {Destination} not linked to any organization",
                transfer.Id, transfer.DestinationId);
        }
    }

    private async Task HandlePayoutCreated(Event stripeEvent)
    {
        await UpsertPayoutAsync(stripeEvent, paidEvent: false);
    }

    private async Task HandlePayoutPaid(Event stripeEvent)
    {
        await UpsertPayoutAsync(stripeEvent, paidEvent: true);
    }

    /// <summary>
    /// Common path for payout.created + payout.paid. The Stripe Event's <c>Account</c>
    /// property identifies the connected account that issued the payout — this is the
    /// only canonical place to find it, since the Payout object itself doesn't carry
    /// the parent account id. <paramref name="paidEvent"/> distinguishes which
    /// timestamp to populate.
    /// </summary>
    private async Task UpsertPayoutAsync(Event stripeEvent, bool paidEvent)
    {
        var payout = stripeEvent.Data.Object as Payout;
        if (payout is null)
        {
            Log.Warning("[Webhook] payout payload not parseable as Payout");
            return;
        }

        var connectedAccountId = stripeEvent.Account;
        if (string.IsNullOrEmpty(connectedAccountId))
        {
            Log.Warning("[Webhook] payout {PayoutId} arrived without an originating account id — skipping", payout.Id);
            return;
        }

        var rawJson = (stripeEvent.Data.Object as StripeEntity)?.ToJson() ?? "{}";
        var paidAt = paidEvent ? (DateTime?)DateTime.UtcNow : null;

        try
        {
            var rowId = await stripeEventProc.UpsertPayoutAsync(
                payout.Id, connectedAccountId,
                (int)payout.Amount, payout.Currency, payout.Status,
                payout.ArrivalDate, paidAt, rawJson);

            Log.Information(
                "[Webhook] payout.{Kind} recorded {RowId} for {PayoutId} ({Amount} {Currency}, status={Status})",
                paidEvent ? "paid" : "created", rowId, payout.Id, payout.Amount, payout.Currency, payout.Status);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0002")
        {
            Log.Warning(
                "[Webhook] payout {PayoutId} — account {AccountId} not linked to any organization",
                payout.Id, connectedAccountId);
        }
    }
}
