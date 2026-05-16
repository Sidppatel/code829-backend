namespace Api.Services;

public interface IPaymentEnrichmentService
{
    Task EnrichAndRecordAsync(string paymentIntentId, string? taxCalculationId);
}
