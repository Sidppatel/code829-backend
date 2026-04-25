namespace Api.Services;

public record PaymentIntentDetails(string Id, int Amount, int AmountReceived, string Status);

public interface IPaymentService
{
    Task<(string PaymentIntentId, string ClientSecret, string Status)> CreatePaymentIntentAsync(
        int amountCents,
        int transferAmountCents,
        string? connectedAccountId,
        string currency = "usd",
        IDictionary<string, string>? metadata = null,
        string? description = null,
        string? statementDescriptorSuffix = null);

    Task<string> ConfirmPaymentAsync(string paymentIntentId);
    Task<PaymentIntentDetails> GetPaymentIntentAsync(string paymentIntentId);
    Task<string> RefundPaymentAsync(string paymentIntentId);
    Task UpdateMetadataAsync(string paymentIntentId, IDictionary<string, string> metadata);
}
