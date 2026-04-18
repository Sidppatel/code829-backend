using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class PurchaseProcedures(EventPlatformDbContext context) : IPurchaseProcedures
{
    public async Task<Guid> CreatePurchaseAsync(Guid userId, Guid eventId, Guid? tableId, int? seats, Guid? eventTicketTypeId, int subtotalCents, int feeCents, int totalCents, string purchaseNumber, string status = "Pending", CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_purchase(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9) AS \"Value\"",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", eventId),
                new NpgsqlParameter("p2", NpgsqlDbType.Uuid) { Value = (object?)tableId ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Integer) { Value = (object?)seats ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Uuid) { Value = (object?)eventTicketTypeId ?? DBNull.Value },
                new NpgsqlParameter("p5", subtotalCents),
                new NpgsqlParameter("p6", feeCents),
                new NpgsqlParameter("p7", totalCents),
                new NpgsqlParameter("p8", purchaseNumber),
                new NpgsqlParameter("p9", status))
            .FirstAsync(ct);

        return result;
    }

    public async Task<Guid> ReserveOpenCapacityAsync(Guid userId, Guid eventId, int seats, Guid? eventTicketTypeId, int subtotalCents, int feeCents, int totalCents, string purchaseNumber, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_reserve_open_capacity(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7) AS \"Value\"",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", eventId),
                new NpgsqlParameter("p2", seats),
                new NpgsqlParameter("p3", NpgsqlDbType.Uuid) { Value = (object?)eventTicketTypeId ?? DBNull.Value },
                new NpgsqlParameter("p4", subtotalCents),
                new NpgsqlParameter("p5", feeCents),
                new NpgsqlParameter("p6", totalCents),
                new NpgsqlParameter("p7", purchaseNumber))
            .FirstAsync(ct);

        return result;
    }

    public async Task ConfirmPurchaseAsync(Guid purchaseId, string qrToken, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_confirm_purchase(@p0, @p1)",
                [purchaseId, qrToken], ct);
    }

    public async Task CancelPurchaseAsync(Guid purchaseId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_cancel_purchase(@p0)",
                [purchaseId], ct);
    }

    public async Task RefundPurchaseAsync(Guid purchaseId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_refund_purchase(@p0)",
                [purchaseId], ct);
    }
}
