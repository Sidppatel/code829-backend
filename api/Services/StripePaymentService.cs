using Serilog;
using Stripe;

namespace Api.Services;

public class StripePaymentService(ISecretsProvider secrets) : IPaymentService
{
    public async Task<(string PaymentIntentId, string ClientSecret, string Status)> CreatePaymentIntentAsync(
        int amountCents,
        int transferAmountCents,
        string? connectedAccountId,
        string currency = "usd",
        IDictionary<string, string>? metadata = null,
        string? description = null,
        string? statementDescriptorSuffix = null)
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

            options.Metadata = new Dictionary<string, string>(metadata);
        }

        if (!string.IsNullOrEmpty(description))
            options.Description = description.Length > 1000 ? description[..1000] : description;

        if (!string.IsNullOrEmpty(statementDescriptorSuffix))
            options.StatementDescriptorSuffix = SanitizeStatementDescriptor(statementDescriptorSuffix);

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

    private static string SanitizeStatementDescriptor(string value)
    {
        var cleaned = new string(value.Where(c => c is not ('<' or '>' or '"' or '\'' or '*')).ToArray()).Trim();
        return cleaned.Length > 22 ? cleaned[..22] : cleaned;
    }
}
