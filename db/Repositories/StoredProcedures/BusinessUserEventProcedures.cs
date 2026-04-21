using Db.Entities;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class BusinessUserEventProcedures(EventPlatformDbContext context) : IBusinessUserEventProcedures
{
    public async Task<Guid> AssignAsync(Guid businessUserId, Guid eventId, Guid? assignedByBusinessUserId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_assign_business_user_event(@p0, @p1, @p2) AS \"Value\"",
                new NpgsqlParameter("p0", businessUserId),
                new NpgsqlParameter("p1", eventId),
                new NpgsqlParameter("p2", NpgsqlDbType.Uuid) { Value = (object?)assignedByBusinessUserId ?? DBNull.Value })
            .FirstAsync(ct);
    }

    public async Task UnassignAsync(Guid businessUserId, Guid eventId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_unassign_business_user_event(@p0, @p1)",
                [businessUserId, eventId], ct);
    }

    public async Task<bool> ExistsAsync(Guid businessUserId, Guid eventId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_business_user_event_exists(@p0, @p1) AS \"Value\"",
                businessUserId, eventId)
            .FirstAsync(ct);
    }

    public async Task<bool> CanAccessEventAsync(Guid businessUserId, Guid eventId, int graceHours = 24, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_staff_can_access_event(@p0, @p1, @p2) AS \"Value\"",
                businessUserId, eventId, graceHours)
            .FirstAsync(ct);
    }

    public async Task<List<BusinessUserEventView>> ListStaffForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.BusinessUserEventViews
            .FromSqlRaw("SELECT * FROM sp_list_staff_for_event({0})", eventId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<Event>> ListEventsForStaffAsync(Guid businessUserId, int graceHours = 24, CancellationToken ct = default)
    {
        return await context.Events
            .FromSqlRaw("SELECT * FROM sp_list_events_for_staff({0}, {1})", businessUserId, graceHours)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
