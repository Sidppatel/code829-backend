using Api.Services;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Stripe;

namespace Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    EventPlatformDbContext context,
    ISettingsService settings,
    IStripeTransactionProcedures stripeTransactionProc,
    IBookingProcedures bookingProc,
    ITaxService taxService
) : ControllerBase
{
    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        Event stripeEvent;
        try
        {
            var webhookSecret = await settings.GetOrDefaultAsync("stripe_webhook_secret", "");
            if (string.IsNullOrEmpty(webhookSecret))
            {
                Log.Error("[Webhook] stripe_webhook_secret not configured — rejecting request");
                return StatusCode(500, "Webhook secret not configured");
            }

            var signature = Request.Headers["Stripe-Signature"].ToString();
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (StripeException ex)
        {
            Log.Warning(ex, "[Webhook] Invalid Stripe signature");
            return BadRequest("Invalid signature");
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
        }

        return Ok();
    }

    private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null) return;

        var txn = await context.StripeTransactions
            .FirstOrDefaultAsync(t => t.PaymentIntentId == paymentIntent.Id);

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

        await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Succeeded");
        await bookingProc.ConfirmBookingAsync(txn.BookingId, "");
        Log.Information("[Webhook] Payment confirmed for booking {BookingId}", txn.BookingId);

        // Enrich with Stripe fee data
        await EnrichTransactionAsync(paymentIntent.Id, txn.TaxCalculationId);

        // Record tax transaction for Stripe Tax reporting
        await RecordTaxTransactionAsync(paymentIntent.Id, txn.TaxCalculationId);
    }

    private async Task EnrichTransactionAsync(string paymentIntentId, string? taxCalculationId)
    {
        try
        {
            var stripeKey = await settings.GetOrDefaultAsync("stripe_secret_key", "");
            if (string.IsNullOrEmpty(stripeKey) || stripeKey == "MOCK_DEV") return;

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

        var txn = await context.StripeTransactions
            .FirstOrDefaultAsync(t => t.PaymentIntentId == paymentIntent.Id);

        if (txn is null) return;

        if (txn.Status == PaymentStatus.Failed)
            return;

        await stripeTransactionProc.UpdateStatusAsync(paymentIntent.Id, "Failed");
        await bookingProc.CancelBookingAsync(txn.BookingId);

        Log.Warning("[Webhook] Payment failed for booking {BookingId}: {Reason}",
            txn.BookingId, paymentIntent.LastPaymentError?.Message ?? "unknown");
    }

    private async Task HandleRefundUpdated(Event stripeEvent)
    {
        var refund = stripeEvent.Data.Object as Refund;
        if (refund?.PaymentIntentId is null) return;

        var txn = await context.StripeTransactions
            .FirstOrDefaultAsync(t => t.PaymentIntentId == refund.PaymentIntentId);

        if (txn is null) return;

        if (refund.Status == "succeeded" && txn.Status != PaymentStatus.Refunded)
        {
            await stripeTransactionProc.UpdateStatusAsync(refund.PaymentIntentId, "Refunded");
            await bookingProc.RefundBookingAsync(txn.BookingId);
            Log.Information("[Webhook] Refund synced for booking {BookingId}", txn.BookingId);
        }
    }
}
