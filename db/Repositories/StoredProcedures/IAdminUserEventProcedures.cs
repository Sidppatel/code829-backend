using Db.Entities;
using Db.Entities.Views;

namespace Db.Repositories.StoredProcedures;

public interface IAdminUserEventProcedures
{
    Task<Guid> AssignAsync(Guid adminUserId, Guid eventId, Guid? assignedByAdminUserId, CancellationToken ct = default);
    Task UnassignAsync(Guid adminUserId, Guid eventId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid adminUserId, Guid eventId, CancellationToken ct = default);
    Task<bool> CanAccessEventAsync(Guid adminUserId, Guid eventId, int graceHours = 24, CancellationToken ct = default);
    Task<List<AdminUserEventView>> ListStaffForEventAsync(Guid eventId, CancellationToken ct = default);
    Task<List<Event>> ListEventsForStaffAsync(Guid adminUserId, int graceHours = 24, CancellationToken ct = default);
}
