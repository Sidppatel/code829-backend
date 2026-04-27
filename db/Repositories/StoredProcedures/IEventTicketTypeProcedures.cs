namespace Db.Repositories.StoredProcedures;

public interface IEventTicketTypeProcedures
{
    Task<Guid> CreateAsync(Guid eventId, string label, int priceCents, int? platformFeeCents, int? maxQuantity, int sortOrder, string? description, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string? label, int? priceCents, int? platformFeeCents, int? maxQuantity, int? sortOrder, bool? isActive, string? description, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> RelinkOrphansAsync(Guid eventId, CancellationToken ct = default);
}
