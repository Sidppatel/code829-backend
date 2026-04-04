using Contracts.DTOs.Tables;
using Contracts.Enums;
using Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

public class TableBookingService(
    EventPlatformDbContext context,
    ISettingsService settings
) : ITableBookingService
{
    public async Task<TableLockDto> LockTableAsync(Guid userId, Guid eventId, Guid tableId)
    {
        var holdMinutes = int.Parse(
            await settings.GetOrDefaultAsync("hold_expiry_minutes", "10") ?? "10");

        var ev = await context.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != EventStatus.Published)
            throw new InvalidOperationException("Event is not available for booking");

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // ── One table per user: release any existing lock first ──
            var existingLock = await context.Tables
                .FromSqlRaw(
                    """
                    SELECT * FROM tables
                    WHERE "EventId" = {0} AND "LockedByUserId" = {1} AND "Status" = {2}
                    FOR UPDATE
                    """,
                    eventId, userId, TableStatus.Locked.ToString())
                .FirstOrDefaultAsync();

            if (existingLock is not null)
            {
                existingLock.Status = TableStatus.Available;
                existingLock.LockedByUserId = null;
                existingLock.LockExpiresAt = null;
                existingLock.UpdatedAt = DateTime.UtcNow;
                Log.Information("[TableLock] Auto-released user {UserId}'s previous lock on table {TableLabel}",
                    userId, existingLock.Label);
            }

            var table = await context.Tables
                .FromSqlRaw(
                    """
                    SELECT * FROM tables
                    WHERE "Id" = {0} AND "EventId" = {1}
                    FOR UPDATE
                    """,
                    tableId, eventId)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Table not found for this event");

            if (table.Status == TableStatus.Booked)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("This table is already booked");
            }

            if (table.Status == TableStatus.Locked && table.LockedByUserId != userId)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("This table is currently held by another user");
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(holdMinutes);

            table.Status = TableStatus.Locked;
            table.LockedByUserId = userId;
            table.LockExpiresAt = expiresAt;
            table.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Load EventTable for capacity/price info
            var eventTable = await context.EventTables.FindAsync(table.EventTableId)
                ?? throw new InvalidOperationException("Table's event table configuration not found");

            Log.Information("[TableLock] User {UserId} locked table {TableLabel} for event {EventId}, expires {ExpiresAt}",
                userId, table.Label, eventId, expiresAt);

            var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_cents", "1500") ?? "1500");
            var feeCents = eventTable.PlatformFeeCents ?? ev.PlatformFeeCents ?? defaultFeeCents;

            return new TableLockDto(
                table.Id,
                table.Label,
                eventId,
                userId,
                "Locked",
                eventTable.Capacity,
                eventTable.PriceCents,
                feeCents,
                expiresAt
            );
        }
        catch (InvalidOperationException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch (Exception)
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    public async Task ReleaseTableLockAsync(Guid userId, Guid eventId, Guid tableId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var table = await context.Tables
                .FromSqlRaw(
                    """
                    SELECT * FROM tables
                    WHERE "Id" = {0} AND "EventId" = {1}
                    FOR UPDATE
                    """,
                    tableId, eventId)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Table not found");

            if (table.Status != TableStatus.Locked)
                throw new InvalidOperationException("Table is not locked");

            if (table.LockedByUserId != userId)
                throw new InvalidOperationException("You do not hold this table");

            table.Status = TableStatus.Available;
            table.LockedByUserId = null;
            table.LockExpiresAt = null;
            table.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("[TableLock] User {UserId} released table {TableLabel} for event {EventId}",
                userId, table.Label, eventId);
        }
        catch (InvalidOperationException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch (Exception)
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    public async Task<List<TableLockDto>> GetUserLockedTablesAsync(Guid userId, Guid eventId)
    {
        var now = DateTime.UtcNow;
        var ev = await context.Events.FindAsync(eventId);
        var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_cents", "1500") ?? "1500");
        var eventFeeFallback = ev?.PlatformFeeCents ?? defaultFeeCents;

        var tables = await context.Tables
            .Include(t => t.EventTable)
            .Where(t => t.EventId == eventId
                && t.Status == TableStatus.Locked
                && t.LockedByUserId == userId
                && t.LockExpiresAt > now)
            .ToListAsync();

        return tables.Select(t => new TableLockDto(
            t.Id,
            t.Label,
            eventId,
            userId,
            "Locked",
            t.EventTable.Capacity,
            t.EventTable.PriceCents,
            t.EventTable.PlatformFeeCents ?? eventFeeFallback,
            t.LockExpiresAt!.Value
        )).ToList();
    }

    public async Task<int> CleanupExpiredLocksAsync()
    {
        var now = DateTime.UtcNow;

        var expiredTables = await context.Tables
            .Where(t => t.Status == TableStatus.Locked && t.LockExpiresAt <= now)
            .ToListAsync();

        foreach (var table in expiredTables)
        {
            table.Status = TableStatus.Available;
            table.LockedByUserId = null;
            table.LockExpiresAt = null;
            table.UpdatedAt = now;
        }

        if (expiredTables.Count > 0)
            await context.SaveChangesAsync();

        var expiredBookings = await context.Bookings
            .Where(b => b.Status == BookingStatus.Pending
                && b.TableId != null
                && b.Table!.Status == TableStatus.Available)
            .ToListAsync();

        foreach (var booking in expiredBookings)
        {
            booking.Status = BookingStatus.Expired;
            booking.UpdatedAt = now;
        }

        if (expiredBookings.Count > 0)
            await context.SaveChangesAsync();

        var totalCleaned = expiredTables.Count + expiredBookings.Count;

        if (expiredTables.Count > 0)
            Log.Information("[TableLockCleanup] Released {TableCount} expired table locks, expired {BookingCount} bookings",
                expiredTables.Count, expiredBookings.Count);

        return totalCleaned;
    }

    public async Task MarkTableBookedAsync(Guid tableId)
    {
        var table = await context.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found");

        table.Status = TableStatus.Booked;
        table.LockedByUserId = null;
        table.LockExpiresAt = null;
        table.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        Log.Information("[TableBooked] Table {TableId} marked as permanently booked", tableId);
    }
}
