using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class StripeTransactionProcedures(EventPlatformDbContext context) : IStripeTransactionProcedures
{
    public async Task<Guid> CreateAsync(Guid bookingId, string intentId, int amountCents,
        int? transferAmountCents = null, string currency = "usd", CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_stripe_transaction(@p0, @p1, @p2, @p3, @p4) AS \"Value\"",
                bookingId, intentId, amountCents, (object?)transferAmountCents ?? DBNull.Value, currency)
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
}
