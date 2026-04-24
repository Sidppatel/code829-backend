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
    ITaxService taxService,
    IPaymentService paymentService,
    IConnectionMultiplexer redis
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
            await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Failed");
            await purchaseProc.CancelPurchaseAsync(txn.PurchaseId);
            return;
        }

        await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Succeeded");
        await purchaseProc.ConfirmPurchaseAsync(txn.PurchaseId, "");
        Log.Information("[Webhook] Payment confirmed for purchase {PurchaseId}", txn.PurchaseId);

        // Enrich with Stripe fee data
        await EnrichTransactionAsync(paymentIntent.Id, txn.TaxCalculationId);

        // Record tax transaction for Stripe Tax reporting
        await RecordTaxTransactionAsync(paymentIntent.Id, txn.TaxCalculationId);
    }

    private async Task EnrichTransactionAsync(string paymentIntentId, string? taxCalculationId)
    {
        try
        {
            var stripeKey = secrets.StripeSecretKey;
            if (string.IsNullOrEmpty(stripeKey)) return;

            var client = new StripeClient(stripeKey);
            var piService = new PaymentIntentService(client);
            var expanded = await piService.GetAsync(paymentIntentId, new PaymentIntentGetOptions
            {
                Expand = ["latest_charge.balance_transaction"]
            });

            var stripeFees = (int)(expanded.LatestCharge?.BalanceTransaction?.Fee ?? 0);
            var totalCharged = (int)expanded.AmountReceived;

            // Get tax amount from the tax calculation if one exists
            var taxAmount = 0;
            if (!string.IsNullOrEmpty(taxCalculationId))
            {
                var calcService = new Stripe.Tax.CalculationService(client);
                var calculation = await calcService.GetAsync(taxCalculationId);
                taxAmount = (int)calculation.TaxAmountExclusive;
            }

            await stripeTransactionProc.EnrichAsync(paymentIntentId, totalCharged, taxAmount, stripeFees);
            Log.Information("[Webhook] Enriched transaction {IntentId}: charged={Charged}, tax={Tax}, fees={Fees}",
                paymentIntentId, totalCharged, taxAmount, stripeFees);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Webhook] Failed to enrich transaction {IntentId} — non-critical", paymentIntentId);
        }
    }

    private async Task RecordTaxTransactionAsync(string paymentIntentId, string? taxCalculationId)
    {
        if (string.IsNullOrEmpty(taxCalculationId)) return;

        try
        {
            var taxTxnId = await taxService.CreateTransactionAsync(taxCalculationId, paymentIntentId);
            await stripeTransactionProc.SetTaxTransactionIdAsync(paymentIntentId, taxTxnId);
            // Mirror the tax transaction id back onto the PaymentIntent metadata so refund
            // paths can reverse the transaction without another DB lookup (Stripe pattern).
            await paymentService.UpdateMetadataAsync(paymentIntentId,
                new Dictionary<string, string> { ["tax_transaction"] = taxTxnId });
            Log.Information("[Webhook] Recorded tax transaction {TaxTxnId} for intent {IntentId}",
                taxTxnId, paymentIntentId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Webhook] Failed to record tax transaction for intent {IntentId} — non-critical", paymentIntentId);
        }
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
}
