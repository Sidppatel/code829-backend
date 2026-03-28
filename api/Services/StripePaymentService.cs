using Serilog;

namespace Api.Services;

/// <summary>
/// Production Stripe payment service. Requires stripe_secret_key from DB settings.
/// In a real deployment, this would use the Stripe SDK. Currently validates config
/// and throws if misconfigured, ensuring mock services are not used in production.
/// </summary>
public class StripePaymentService(ISettingsService settings) : IPaymentService
{
    public async Task<(string PaymentIntentId, string Status)> CreatePaymentIntentAsync(int amountCents, string currency = "usd")
    {
        var key = await settings.GetAsync("stripe_secret_key");
        if (key == "MOCK_DEV")
            throw new InvalidOperationException("Stripe is not configured — stripe_secret_key is still MOCK_DEV");

        // Production implementation would call Stripe SDK here:
        // var options = new PaymentIntentCreateOptions { Amount = amountCents, Currency = currency };
        // var service = new PaymentIntentService();
        // var intent = await service.CreateAsync(options);
        // return (intent.Id, intent.Status);
        Log.Information("[Stripe] CreatePaymentIntent: {Amount} {Currency}", amountCents, currency);
        throw new NotImplementedException("Stripe SDK integration pending — configure stripe_secret_key");
    }

    public async Task<string> ConfirmPaymentAsync(string paymentIntentId)
    {
        var key = await settings.GetAsync("stripe_secret_key");
        if (key == "MOCK_DEV")
            throw new InvalidOperationException("Stripe is not configured");

        Log.Information("[Stripe] ConfirmPayment: {IntentId}", paymentIntentId);
        throw new NotImplementedException("Stripe SDK integration pending");
    }

    public async Task<string> RefundPaymentAsync(string paymentIntentId)
    {
        var key = await settings.GetAsync("stripe_secret_key");
        if (key == "MOCK_DEV")
            throw new InvalidOperationException("Stripe is not configured");

        Log.Information("[Stripe] RefundPayment: {IntentId}", paymentIntentId);
        throw new NotImplementedException("Stripe SDK integration pending");
    }
}
