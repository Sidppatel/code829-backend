using Serilog;
using Stripe;

namespace Api.Services;

/// <summary>
/// Production Stripe payment service using destination charges (Stripe Connect).
/// The total amount is charged to the customer. The organizer receives (total - applicationFee)
/// via transfer_data. The platform keeps the applicationFee minus Stripe processing costs.
/// </summary>
public class StripePaymentService(ISettingsService settings) : IPaymentService
{
    public async Task<(string PaymentIntentId, string ClientSecret, string Status)> CreatePaymentIntentAsync(
        int amountCents,
        int applicationFeeCents,
        string? connectedAccountId,
        string currency = "usd")
    {
        var client = await GetClientAsync();

        var options = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
        };

        if (!string.IsNullOrEmpty(connectedAccountId))
        {
            options.ApplicationFeeAmount = applicationFeeCents;
            options.TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = connectedAccountId
            };
        }

        try
        {
            var service = new PaymentIntentService(client);
            var intent = await service.CreateAsync(options);
            Log.Information(
                "[Stripe] Created PaymentIntent {IntentId} for {Amount} {Currency}, fee={Fee}, dest={Dest}",
                intent.Id, amountCents, currency, applicationFeeCents, connectedAccountId ?? "none");
            return (intent.Id, intent.ClientSecret, intent.Status);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Stripe] Failed to create PaymentIntent");
            throw MapStripeException(ex);
        }
    }

    public async Task<string> ConfirmPaymentAsync(string paymentIntentId)
    {
        var client = await GetClientAsync();

        try
        {
            var service = new PaymentIntentService(client);
            var intent = await service.GetAsync(paymentIntentId);
            Log.Information("[Stripe] Retrieved PaymentIntent {IntentId}, status: {Status}",
                paymentIntentId, intent.Status);
            return intent.Status;
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Stripe] Failed to confirm PaymentIntent {IntentId}", paymentIntentId);
            throw MapStripeException(ex);
        }
    }

    public async Task<string> RefundPaymentAsync(string paymentIntentId)
    {
        var client = await GetClientAsync();

        try
        {
            var service = new RefundService(client);
            var refund = await service.CreateAsync(new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                RefundApplicationFee = true,
                ReverseTransfer = true
            });
            Log.Information("[Stripe] Refund {RefundId} created for PaymentIntent {IntentId}",
                refund.Id, paymentIntentId);
            return refund.Status;
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Stripe] Failed to refund PaymentIntent {IntentId}", paymentIntentId);
            throw MapStripeException(ex);
        }
    }

    private async Task<StripeClient> GetClientAsync()
    {
        var key = await settings.GetAsync("stripe_secret_key");
        if (string.IsNullOrEmpty(key) || key == "MOCK_DEV")
            throw new InvalidOperationException("Stripe is not configured — set stripe_secret_key in settings");

        return new StripeClient(key);
    }

    private static Exception MapStripeException(StripeException ex)
    {
        return ex.StripeError?.Type switch
        {
            "card_error" => new InvalidOperationException($"Payment declined: {ex.StripeError.Message}", ex),
            "invalid_request_error" => new ArgumentException($"Invalid payment request: {ex.StripeError.Message}", ex),
            _ => new InvalidOperationException($"Payment processing error: {ex.Message}", ex)
        };
    }
}
