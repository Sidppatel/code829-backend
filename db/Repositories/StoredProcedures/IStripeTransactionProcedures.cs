namespace Db.Repositories.StoredProcedures;

public interface IStripeTransactionProcedures
{
    Task<Guid> CreateAsync(Guid bookingId, string intentId, int amountCents,
        int? transferAmountCents = null, string currency = "usd", CancellationToken ct = default);

    Task UpdateStatusAsync(string intentId, string status, CancellationToken ct = default);

    Task EnrichAsync(string intentId, int totalChargedCents, int taxAmountCents,
        int stripeFeesCents, CancellationToken ct = default);
}
