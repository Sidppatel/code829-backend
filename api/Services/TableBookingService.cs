using Contracts.DTOs.Tables;
using Db;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

public class TableBookingService(
    EventPlatformDbContext context,
    ITableProcedures tableProc,
    ISettingsService settings
) : ITableBookingService
{
    public async Task<TableLockDto> LockTableAsync(Guid userId, Guid eventId, Guid tableId)
    {
        var holdMinutes = int.Parse(
            await settings.GetOrDefaultAsync("hold_expiry_minutes", "10") ?? "10");

        var userExists = await context.UserProfileViews.AsNoTracking().AnyAsync(u => u.UserId == userId);
        if (!userExists)
            throw new KeyNotFoundException("User not found");

        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != "Published")
            throw new InvalidOperationException("Event is not available for purchase");

        var result = await tableProc.LockTableAsync(userId, eventId, tableId, holdMinutes);

        var table = await context.TableViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TableId == tableId && t.EventId == eventId)
            ?? throw new InvalidOperationException("Table lock succeeded but table not found in view");

        var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_grid_cents", "2500") ?? "2500");
        var feeCents = table.PlatformFeeCents ?? defaultFeeCents;

        Log.Information("[TableLock] User {UserId} locked table {TableLabel} for event {EventId}, expires {ExpiresAt}",
            userId, result.Label, eventId, result.LockExpiresAt);

        return new TableLockDto(
            result.Id,
            result.Label,
            eventId,
            userId,
            "Locked",
            table.Capacity,
            table.PriceCents,
            feeCents,
            table.PriceCents + feeCents,
            result.LockExpiresAt
        );
    }

    public async Task ReleaseTableLockAsync(Guid userId, Guid eventId, Guid tableId)
    {
        var released = await tableProc.ReleaseTableLockAsync(userId, eventId, tableId);
        if (!released)
            throw new InvalidOperationException("Table is not locked by you or not found");

        Log.Information("[TableLock] User {UserId} released table {TableId} for event {EventId}",
            userId, tableId, eventId);
    }

    public async Task<List<TableLockDto>> GetUserLockedTablesAsync(Guid userId, Guid eventId)
    {
        var now = DateTime.UtcNow;
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId);
        var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_grid_cents", "2500") ?? "2500");
        var eventFeeFallback = defaultFeeCents;

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == eventId
                && t.Status == "Locked"
                && t.LockedByUserId == userId
                && t.LockExpiresAt > now)
            .ToListAsync();

        return tables.Select(t =>
        {
            var fee = t.PlatformFeeCents ?? eventFeeFallback;
            return new TableLockDto(
                t.TableId,
                t.Label,
                eventId,
                userId,
                "Locked",
                t.Capacity,
                t.PriceCents,
                fee,
                t.PriceCents + fee,
                t.LockExpiresAt!.Value
            );
        }).ToList();
    }

    public async Task<int> CleanupExpiredLocksAsync()
    {
        var count = await tableProc.CleanupExpiredLocksAsync();
        if (count > 0)
            Log.Information("[TableLockCleanup] Cleaned up {Count} expired locks", count);
        return count;
    }

    public async Task MarkTableBookedAsync(Guid tableId)
    {
        await tableProc.MarkTableBookedAsync(tableId);
        Log.Information("[TableBooked] Table {TableId} marked as permanently booked", tableId);
    }
}
