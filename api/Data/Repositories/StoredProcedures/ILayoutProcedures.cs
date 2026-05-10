using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public record GridOverlapPair(Guid TableAId, Guid TableBId);

public interface ILayoutProcedures
{

    Task<Guid> CreateTableTemplateAsync(string name, int capacity, string shape, string? color, int priceCents, int defaultRowSpan = 1, int defaultColSpan = 1, CancellationToken ct = default);
    Task UpdateTableTemplateAsync(Guid id, string? name, int? capacity, string? shape, string? color, int? priceCents, bool? isActive, int? defaultRowSpan = null, int? defaultColSpan = null, CancellationToken ct = default);
    Task DeactivateTableTemplateAsync(Guid id, CancellationToken ct = default);
    Task<TableTemplate?> GetTableTemplateByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TableTemplate>> ListTableTemplatesAsync(CancellationToken ct = default);
    Task<List<TableTemplate>> ListActiveTableTemplatesByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<Event?> GetEventByIdForLayoutAsync(Guid id, CancellationToken ct = default);
    Task UpdateEventGridAsync(Guid id, int? gridRows, int? gridCols, CancellationToken ct = default);

    Task UpdateEventTableAsync(Guid id, string? label, int? capacity, string? shape, string? color, int? priceCents, bool? isActive, int? platformFeeCents = null, int? rowSpan = null, int? colSpan = null, CancellationToken ct = default);
    Task DeleteEventTableAsync(Guid id, CancellationToken ct = default);
    Task<EventTable?> GetEventTableByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<EventTable>> ListEventTablesForEventAsync(Guid eventId, CancellationToken ct = default);
    Task<HashSet<Guid>> ListExistingEventTableTemplateIdsAsync(Guid eventId, CancellationToken ct = default);

    Task UpdateTableAsync(Guid id, string? label, Guid? eventTableId, int? gridRow, int? gridCol, bool? isActive, int? sortOrder, int? rowSpan = null, int? colSpan = null, CancellationToken ct = default);
    Task DeleteTableAsync(Guid id, CancellationToken ct = default);
    Task<Table?> GetTableByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Table>> ListTablesForEventAsync(Guid eventId, CancellationToken ct = default);

    Task<bool> EventHasActivePurchasesAsync(Guid eventId, CancellationToken ct = default);
    Task<bool> EventTableHasActivePurchasesAsync(Guid eventId, Guid eventTableId, CancellationToken ct = default);
    Task<bool> EventTableHasLockedTablesAsync(Guid eventTableId, CancellationToken ct = default);
    Task<HashSet<Guid>> GetLockedTableIdsAsync(Guid eventId, CancellationToken ct = default);

    Task<List<GridOverlapPair>> CheckGridOverlapAsync(Guid eventId, Guid? skipTableId = null, CancellationToken ct = default);

    Task SaveEventLayoutAsync(Guid eventId, int? gridRows, int? gridCols, string tablesJson, Guid[] lockedIds, CancellationToken ct = default);
}
