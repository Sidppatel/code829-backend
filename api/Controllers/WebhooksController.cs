using Api.Services;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Stripe;

namespace Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    EventPlatformDbContext context,
    ISettingsService settings
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

        var payment = await context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.PaymentIntentId == paymentIntent.Id);

        if (payment is null)
        {
            Log.Warning("[Webhook] No payment found for intent {IntentId}", paymentIntent.Id);
            return;
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            Log.Information("[Webhook] Payment {IntentId} already confirmed (idempotent skip)", paymentIntent.Id);
            return;
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = DateTime.UtcNow;
        payment.Booking.Status = BookingStatus.Paid;

        await context.SaveChangesAsync();
        Log.Information("[Webhook] Payment confirmed for booking {BookingId}", payment.BookingId);
    }

    private async Task HandlePaymentIntentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null) return;

        var payment = await context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.PaymentIntentId == paymentIntent.Id);

        if (payment is null) return;

        if (payment.Status == PaymentStatus.Failed)
            return;

        payment.Status = PaymentStatus.Failed;
        payment.Booking.Status = BookingStatus.Cancelled;
        await context.SaveChangesAsync();

        Log.Warning("[Webhook] Payment failed for booking {BookingId}: {Reason}",
            payment.BookingId, paymentIntent.LastPaymentError?.Message ?? "unknown");
    }

    private async Task HandleRefundUpdated(Event stripeEvent)
    {
        var refund = stripeEvent.Data.Object as Refund;
        if (refund?.PaymentIntentId is null) return;

        var payment = await context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.PaymentIntentId == refund.PaymentIntentId);

        if (payment is null) return;

        if (refund.Status == "succeeded" && payment.Status != PaymentStatus.Refunded)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.RefundedAt = DateTime.UtcNow;
            payment.RefundId = refund.Id;
            payment.Booking.Status = BookingStatus.Refunded;
            await context.SaveChangesAsync();
            Log.Information("[Webhook] Refund synced for booking {BookingId}", payment.BookingId);
        }
    }
}
