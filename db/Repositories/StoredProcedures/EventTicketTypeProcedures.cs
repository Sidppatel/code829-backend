using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class EventTicketTypeProcedures(EventPlatformDbContext context) : IEventTicketTypeProcedures
{
    public async Task<Guid> CreateAsync(Guid eventId, string label, int priceCents, int? platformFeeCents, int? maxQuantity, int sortOrder, string? description, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_event_ticket_type(@p0, @p1, @p2, @p3, @p4, @p5, @p6) AS \"Value\"",
                new NpgsqlParameter("@p0", eventId),
                new NpgsqlParameter("@p1", label),
                new NpgsqlParameter("@p2", priceCents),
                new NpgsqlParameter("@p3", NpgsqlDbType.Integer) { Value = (object?)platformFeeCents ?? DBNull.Value },
                new NpgsqlParameter("@p4", NpgsqlDbType.Integer) { Value = (object?)maxQuantity ?? DBNull.Value },
                new NpgsqlParameter("@p5", sortOrder),
                new NpgsqlParameter("@p6", NpgsqlDbType.Text) { Value = (object?)description ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateAsync(Guid id, string? label, int? priceCents, int? platformFeeCents, int? maxQuantity, int? sortOrder, bool? isActive, string? description, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_event_ticket_type(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                [
                    new NpgsqlParameter("@p0", id),
                    new NpgsqlParameter("@p1", NpgsqlDbType.Text) { Value = (object?)label ?? DBNull.Value },
                    new NpgsqlParameter("@p2", NpgsqlDbType.Integer) { Value = (object?)priceCents ?? DBNull.Value },
                    new NpgsqlParameter("@p3", NpgsqlDbType.Integer) { Value = (object?)platformFeeCents ?? DBNull.Value },
                    new NpgsqlParameter("@p4", NpgsqlDbType.Integer) { Value = (object?)maxQuantity ?? DBNull.Value },
                    new NpgsqlParameter("@p5", NpgsqlDbType.Integer) { Value = (object?)sortOrder ?? DBNull.Value },
                    new NpgsqlParameter("@p6", NpgsqlDbType.Boolean) { Value = (object?)isActive ?? DBNull.Value },
                    new NpgsqlParameter("@p7", NpgsqlDbType.Text) { Value = (object?)description ?? DBNull.Value }
                ], ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_delete_event_ticket_type(@p0)",
                [new NpgsqlParameter("@p0", id)], ct);
    }

    public async Task<int> RelinkOrphansAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_relink_orphan_ticket_types(@p0) AS \"Value\"",
                new NpgsqlParameter("@p0", eventId))
            .FirstAsync(ct);
    }
}
