using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public interface IStripeTransactionProcedures
{
    Task<Guid> CreateAsync(Guid purchaseId, string intentId, int amountCents,
        int? transferAmountCents = null, string? taxCalculationId = null,
        string currency = "usd", CancellationToken ct = default);

    Task UpdateStatusAsync(string intentId, string status, CancellationToken ct = default);

    Task EnrichAsync(string intentId, int totalChargedCents, int taxAmountCents,
        int stripeFeesCents, CancellationToken ct = default);

    Task SetTaxTransactionIdAsync(string intentId, string taxTransactionId, CancellationToken ct = default);

    Task<StripeTransaction?> GetByPaymentIntentAsync(string intentId, CancellationToken ct = default);
}
