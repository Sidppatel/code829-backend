using Serilog;
using Stripe;

namespace Api.Services;

/// <summary>
/// Production Stripe payment service using destination charges (Stripe Connect).
/// The total amount is charged to the customer. The organizer receives transferAmountCents
/// via transfer_data.amount. The platform keeps everything else minus Stripe processing costs.
/// When Stripe Tax is enabled, tax is calculated and added on top by Stripe.
/// </summary>
public class StripePaymentService(ISecretsProvider secrets) : IPaymentService
{
    public async Task<(string PaymentIntentId, string ClientSecret, string Status)> CreatePaymentIntentAsync(
        int amountCents,
        int transferAmountCents,
        string? connectedAccountId,
        string currency = "usd",
        IDictionary<string, string>? metadata = null)
    {
        var client = await GetClientAsync();

        var options = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
        };

        if (metadata is { Count: > 0 })
        {
            // Stripe caps metadata keys at 50 and values at 500 chars; short keys/values below are well within limits.
            options.Metadata = new Dictionary<string, string>(metadata);
        }

        if (!string.IsNullOrEmpty(connectedAccountId))
        {
            options.TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = connectedAccountId,
                Amount = transferAmountCents
            };
        }

        try
        {
            var service = new PaymentIntentService(client);
            var intent = await service.CreateAsync(options);
            Log.Information(
                "[Stripe] Created PaymentIntent {IntentId} for {Amount} {Currency}, transfer={Transfer}, dest={Dest}",
                intent.Id, amountCents, currency, transferAmountCents, connectedAccountId ?? "none");
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

    public async Task<PaymentIntentDetails> GetPaymentIntentAsync(string paymentIntentId)
    {
        var client = await GetClientAsync();

        try
        {
            var service = new PaymentIntentService(client);
            var intent = await service.GetAsync(paymentIntentId);
            return new PaymentIntentDetails(
                intent.Id,
                (int)intent.Amount,
                (int)intent.AmountReceived,
                intent.Status);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Stripe] Failed to fetch PaymentIntent {IntentId}", paymentIntentId);
            throw MapStripeException(ex);
        }
    }

    public async Task UpdateMetadataAsync(string paymentIntentId, IDictionary<string, string> metadata)
    {
        if (metadata.Count == 0) return;
        var client = await GetClientAsync();

        try
        {
            var service = new PaymentIntentService(client);
            await service.UpdateAsync(paymentIntentId, new PaymentIntentUpdateOptions
            {
                Metadata = new Dictionary<string, string>(metadata)
            });
        }
        catch (StripeException ex)
        {
            Log.Warning(ex, "[Stripe] Failed to update metadata on PaymentIntent {IntentId}", paymentIntentId);
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

    private Task<StripeClient> GetClientAsync()
    {
        var key = secrets.StripeSecretKey;
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("Stripe is not configured — set STRIPE_SECRET_KEY environment variable");

        return Task.FromResult(new StripeClient(key));
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
