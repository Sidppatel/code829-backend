namespace Db.Repositories.StoredProcedures;

public record TableLockResult(Guid Id, string Label, DateTime LockExpiresAt);

public interface ITableProcedures
{
    Task<Guid> CreateEventTableAsync(
        Guid? eventId, string? label, int? capacity, string? shape,
        string? color, int? priceCents, int? platformFeeCents, Guid? templateId);

    Task<Guid> CreateTableAsync(
        Guid? eventTableId, Guid? eventId, string? label,
        int? gridRow, int? gridCol, int? sortOrder);

    Task<TableLockResult> LockTableAsync(
        Guid? userId, Guid? eventId, Guid? tableId, int? holdMinutes);

    Task<bool> ReleaseTableLockAsync(Guid? userId, Guid? eventId, Guid? tableId);

    Task MarkTableBookedAsync(Guid? tableId);

    Task<int> CleanupExpiredLocksAsync();
}
