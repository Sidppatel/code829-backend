using Contracts.DTOs.Tables;

namespace Api.Services;

public interface ITableBookingService
{
    Task<TableLockDto> LockTableAsync(Guid userId, Guid eventId, Guid tableId);
    Task ReleaseTableLockAsync(Guid userId, Guid eventId, Guid tableId);
    Task<List<TableLockDto>> GetUserLockedTablesAsync(Guid userId, Guid eventId);
    Task<int> CleanupExpiredLocksAsync();
    Task MarkTableBookedAsync(Guid tableId);
}
