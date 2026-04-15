using Serilog;

namespace Api.Services;

public class MockTaxService : ITaxService
{
    public Task<TaxCalculationResult> CalculateAsync(
        int amountCents,
        string currency,
        string line1,
        string city,
        string state,
        string postalCode,
        string country = "US",
        CancellationToken ct = default)
    {
        var calcId = $"taxcalc_mock_{Guid.NewGuid():N}";
        Log.Information("[MockTax] Calculated tax for {Amount} cents at {Location}: no tax applied",
            amountCents, $"{city}, {state} {postalCode}");
        return Task.FromResult(new TaxCalculationResult(calcId, amountCents, 0));
    }

    public Task<string> CreateTransactionAsync(string calculationId, string reference, CancellationToken ct = default)
    {
        var txnId = $"tax_mock_{Guid.NewGuid():N}";
        Log.Information("[MockTax] Created mock tax transaction {TxnId}", txnId);
        return Task.FromResult(txnId);
    }

    public Task CreateReversalAsync(string taxTransactionId, string reference, CancellationToken ct = default)
    {
        Log.Information("[MockTax] Reversed mock tax transaction {TxnId}", taxTransactionId);
        return Task.CompletedTask;
    }
}
