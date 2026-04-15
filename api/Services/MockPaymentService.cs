using Serilog;

namespace Api.Services;

/// <summary>
/// Development payment service that auto-confirms payments without any external calls.
/// Returns mock payment intent IDs for tracking.
/// </summary>
public class MockPaymentService : IPaymentService
{
    public Task<(string PaymentIntentId, string ClientSecret, string Status)> CreatePaymentIntentAsync(
        int amountCents,
        int transferAmountCents,
        string? connectedAccountId,
        string currency = "usd")
    {
        var intentId = $"pi_mock_{Guid.NewGuid():N}";
        var clientSecret = $"{intentId}_secret_mock";
        Log.Information("[MockPayment] Created intent {IntentId} for {Amount} cents, transfer={Transfer}, dest={Dest}",
            intentId, amountCents, transferAmountCents, connectedAccountId ?? "none");
        return Task.FromResult((intentId, clientSecret, "requires_payment_method"));
    }

    public Task<string> ConfirmPaymentAsync(string paymentIntentId)
    {
        Log.Information("[MockPayment] Confirmed {IntentId}", paymentIntentId);
        return Task.FromResult("succeeded");
    }

    public Task<string> RefundPaymentAsync(string paymentIntentId)
    {
        Log.Information("[MockPayment] Refunded {IntentId}", paymentIntentId);
        return Task.FromResult("refunded");
    }
}
