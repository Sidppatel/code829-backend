using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class EventTicketTypeProcedures(EventPlatformDbContext context) : IEventTicketTypeProcedures
{
    public async Task<Guid> CreateAsync(Guid eventId, string label, int priceCents, int? platformFeeCents, int? maxQuantity, int sortOrder, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_event_ticket_type(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                eventId, label, priceCents,
                (object?)platformFeeCents ?? DBNull.Value,
                (object?)maxQuantity ?? DBNull.Value,
                sortOrder)
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateAsync(Guid id, string? label, int? priceCents, int? platformFeeCents, int? maxQuantity, int? sortOrder, bool? isActive, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_event_ticket_type(@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                [
                    id,
                    (object?)label ?? DBNull.Value,
                    (object?)priceCents ?? DBNull.Value,
                    (object?)platformFeeCents ?? DBNull.Value,
                    (object?)maxQuantity ?? DBNull.Value,
                    (object?)sortOrder ?? DBNull.Value,
                    (object?)isActive ?? DBNull.Value
                ], ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_delete_event_ticket_type(@p0)",
                [id], ct);
    }
}
