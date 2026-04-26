namespace Db.Repositories.StoredProcedures;

public record TableLockResult(Guid Id, string Label, DateTime LockExpiresAt);

public interface ITableProcedures
{
    Task<Guid> CreateEventTableAsync(
        Guid? eventId, string? label, int? capacity, string? shape,
        string? color, int? priceCents, int? platformFeeCents, Guid? templateId,
        int? rowSpan = null, int? colSpan = null);

    Task<Guid> CreateTableAsync(
        Guid? eventTableId, Guid? eventId, string? label,
        int? gridRow, int? gridCol, int? sortOrder,
        int rowSpan = 1, int colSpan = 1);

    Task<TableLockResult> LockTableAsync(
        Guid? userId, Guid? eventId, Guid? tableId, int? holdMinutes);

    Task<bool> ReleaseTableLockAsync(Guid? userId, Guid? eventId, Guid? tableId);

    Task MarkTableBookedAsync(Guid? tableId);

    Task<int> CleanupExpiredLocksAsync();
}
