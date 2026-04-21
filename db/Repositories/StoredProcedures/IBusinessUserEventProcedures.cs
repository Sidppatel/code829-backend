using Db.Entities;
using Db.Entities.Views;

namespace Db.Repositories.StoredProcedures;

public interface IBusinessUserEventProcedures
{
    Task<Guid> AssignAsync(Guid businessUserId, Guid eventId, Guid? assignedByBusinessUserId, CancellationToken ct = default);
    Task UnassignAsync(Guid businessUserId, Guid eventId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid businessUserId, Guid eventId, CancellationToken ct = default);
    Task<bool> CanAccessEventAsync(Guid businessUserId, Guid eventId, int graceHours = 24, CancellationToken ct = default);
    Task<List<BusinessUserEventView>> ListStaffForEventAsync(Guid eventId, CancellationToken ct = default);
    Task<List<Event>> ListEventsForStaffAsync(Guid businessUserId, int graceHours = 24, CancellationToken ct = default);
}
