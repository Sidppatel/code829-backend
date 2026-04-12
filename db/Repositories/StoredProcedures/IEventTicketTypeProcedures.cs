namespace Db.Repositories.StoredProcedures;

public interface IEventTicketTypeProcedures
{
    Task<Guid> CreateAsync(Guid eventId, string label, int priceCents, int? platformFeeCents, int? maxQuantity, int sortOrder, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string? label, int? priceCents, int? platformFeeCents, int? maxQuantity, int? sortOrder, bool? isActive, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
