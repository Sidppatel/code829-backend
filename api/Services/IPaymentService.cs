namespace Api.Services;

public interface IPaymentService
{
    Task<(string PaymentIntentId, string ClientSecret, string Status)> CreatePaymentIntentAsync(int amountCents, string currency = "usd");
    Task<string> ConfirmPaymentAsync(string paymentIntentId);
    Task<string> RefundPaymentAsync(string paymentIntentId);
}
