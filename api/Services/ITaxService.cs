namespace Api.Services;

public record TaxCalculationResult(
    string CalculationId,
    int AmountTotal,
    int TaxAmountExclusive
);

public interface ITaxService
{
    Task<TaxCalculationResult> CalculateAsync(
        int amountCents,
        string currency,
        string line1,
        string city,
        string state,
        string postalCode,
        string country = "US",
        CancellationToken ct = default);

    Task<string> CreateTransactionAsync(string calculationId, string reference, CancellationToken ct = default);

    Task CreateReversalAsync(string taxTransactionId, string reference, CancellationToken ct = default);
}
