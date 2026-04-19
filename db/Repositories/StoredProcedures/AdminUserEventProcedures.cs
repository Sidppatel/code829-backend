using Db.Entities;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class AdminUserEventProcedures(EventPlatformDbContext context) : IAdminUserEventProcedures
{
    public async Task<Guid> AssignAsync(Guid adminUserId, Guid eventId, Guid? assignedByAdminUserId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_assign_admin_user_event(@p0, @p1, @p2) AS \"Value\"",
                new NpgsqlParameter("p0", adminUserId),
                new NpgsqlParameter("p1", eventId),
                new NpgsqlParameter("p2", NpgsqlDbType.Uuid) { Value = (object?)assignedByAdminUserId ?? DBNull.Value })
            .FirstAsync(ct);
    }

    public async Task UnassignAsync(Guid adminUserId, Guid eventId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_unassign_admin_user_event(@p0, @p1)",
                [adminUserId, eventId], ct);
    }

    public async Task<bool> ExistsAsync(Guid adminUserId, Guid eventId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_admin_user_event_exists(@p0, @p1) AS \"Value\"",
                adminUserId, eventId)
            .FirstAsync(ct);
    }

    public async Task<bool> CanAccessEventAsync(Guid adminUserId, Guid eventId, int graceHours = 24, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_staff_can_access_event(@p0, @p1, @p2) AS \"Value\"",
                adminUserId, eventId, graceHours)
            .FirstAsync(ct);
    }

    public async Task<List<AdminUserEventView>> ListStaffForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.AdminUserEventViews
            .FromSqlRaw("SELECT * FROM sp_list_staff_for_event({0})", eventId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<Event>> ListEventsForStaffAsync(Guid adminUserId, int graceHours = 24, CancellationToken ct = default)
    {
        return await context.Events
            .FromSqlRaw("SELECT * FROM sp_list_events_for_staff({0}, {1})", adminUserId, graceHours)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
