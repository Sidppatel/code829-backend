using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class StripeEventProcedures(EventPlatformDbContext context) : IStripeEventProcedures
{
    public async Task<Guid> InsertTransferAsync(
        string stripeTransferId,
        string stripeAccountId,
        string? paymentIntentId,
        int amountCents,
        string? currency,
        string rawEventJson,
        CancellationToken ct = default)
    {

        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_insert_stripe_transfer(@p0, @p1, @p2, @p3, @p4, @p5::jsonb) AS \"Value\"",
                new NpgsqlParameter("p0", stripeTransferId),
                new NpgsqlParameter("p1", stripeAccountId),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)paymentIntentId ?? DBNull.Value },
                new NpgsqlParameter("p3", amountCents),
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)currency ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = rawEventJson })
            .FirstAsync(ct);
    }

    public async Task<Guid> UpsertPayoutAsync(
        string stripePayoutId,
        string stripeAccountId,
        int amountCents,
        string? currency,
        string status,
        DateTime? arrivalDate,
        DateTime? paidAt,
        string rawEventJson,
        CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_update_stripe_payout(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7::jsonb) AS \"Value\"",
                new NpgsqlParameter("p0", stripePayoutId),
                new NpgsqlParameter("p1", stripeAccountId),
                new NpgsqlParameter("p2", amountCents),
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)currency ?? DBNull.Value },
                new NpgsqlParameter("p4", status),
                new NpgsqlParameter("p5", NpgsqlDbType.TimestampTz) { Value = (object?)arrivalDate ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.TimestampTz) { Value = (object?)paidAt ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = rawEventJson })
            .FirstAsync(ct);
    }
}
