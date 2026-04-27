using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class StripeTransactionProcedures(EventPlatformDbContext context) : IStripeTransactionProcedures
{
    public async Task<Guid> CreateAsync(Guid purchaseId, string intentId, int amountCents,
        int? transferAmountCents = null, string? taxCalculationId = null,
        string currency = "usd", CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_stripe_transaction(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                purchaseId, intentId, amountCents,
                (object?)transferAmountCents ?? DBNull.Value,
                (object?)taxCalculationId ?? DBNull.Value,
                currency)
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateStatusAsync(string intentId, string status, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_stripe_transaction_status(@p0, @p1)",
                [intentId, status], ct);
    }

    public async Task EnrichAsync(string intentId, int totalChargedCents, int taxAmountCents,
        int stripeFeesCents, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_enrich_stripe_transaction(@p0, @p1, @p2, @p3)",
                [intentId, totalChargedCents, taxAmountCents, stripeFeesCents], ct);
    }

    public async Task SetTaxTransactionIdAsync(string intentId, string taxTransactionId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_set_stripe_tax_transaction_id(@p0, @p1)",
                [intentId, taxTransactionId], ct);
    }

    public async Task<Db.Entities.StripeTransaction?> GetByPaymentIntentAsync(string intentId, CancellationToken ct = default)
    {
        return await context.StripeTransactions
            .FromSqlRaw("SELECT * FROM sp_get_stripe_transaction_by_intent({0})", intentId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }
}
