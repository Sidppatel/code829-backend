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

            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            Log.Warning(ex, "[Webhook] Invalid Stripe signature");
            return BadRequest("Invalid signature");
        }

        var dedupeKey = $"stripe-webhook:{stripeEvent.Id}";
        var inflightKey = $"stripe-webhook:inflight:{stripeEvent.Id}";
        var db = redis.GetDatabase();
        var firstSeen = await db.StringSetAsync(dedupeKey, "1", DedupeTtl, When.NotExists);
        if (!firstSeen)
        {
            Log.Information("[Webhook] Duplicate event {EventId} ({EventType}) — skipping", stripeEvent.Id, stripeEvent.Type);
            return Ok();
        }

        var gotInflight = await db.StringSetAsync(inflightKey, "1", InflightTtl, When.NotExists);
        if (!gotInflight)
        {
            Log.Warning("[Webhook] Event {EventId} already in-flight — returning 409 so Stripe retries", stripeEvent.Id);

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
        await purchaseProc.ConfirmPurchaseAsync(txn.PurchaseId, PurchaseService.GenerateQrToken());
        Log.Information("[Webhook] Payment confirmed for purchase {PurchaseId}", txn.PurchaseId);

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
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0002")
        {

            Log.Warning(
                "[Webhook] account.updated for unknown account {AccountId} — no organization linked",
                account.Id);
        }
    }

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

        if (sourcePi is not null && !string.IsNullOrEmpty(secrets.StripeSecretKey))
        {

            var description = !string.IsNullOrEmpty(sourcePi.Description)
                ? sourcePi.Description
                : Services.PaymentDescriptions.FromMetadata(sourcePi.Metadata);

            if (!string.IsNullOrEmpty(description))
            {
                var client = new StripeClient(secrets.StripeSecretKey);
                var metadata = sourcePi.Metadata is { Count: > 0 }
                    ? new Dictionary<string, string>(sourcePi.Metadata)
                    : null;

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

                }

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
