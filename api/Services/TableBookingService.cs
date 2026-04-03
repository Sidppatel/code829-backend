using Contracts.DTOs.Tables;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

public class TableBookingService(
    EventPlatformDbContext context,
    ISettingsService settings
) : ITableBookingService
{
    public async Task<TableLockDto> LockTableAsync(Guid userId, Guid eventId, Guid tableId, Guid ticketTypeId)
    {
        var holdMinutes = int.Parse(
            await settings.GetOrDefaultAsync("hold_expiry_minutes", "10") ?? "10");

        var ev = await context.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != EventStatus.Published)
            throw new InvalidOperationException("Event is not available for booking");

        var ticketType = await context.TicketTypes.FindAsync(ticketTypeId)
            ?? throw new KeyNotFoundException("Ticket type not found");

        if (ticketType.EventId != eventId)
            throw new InvalidOperationException("Ticket type does not belong to this event");

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Row-level lock on the table to prevent concurrent lock attempts
            var table = await context.Tables
                .FromSqlRaw(
                    """
                    SELECT * FROM tables
                    WHERE "Id" = {0} AND "EventId" = {1}
                    FOR UPDATE
                    """,
                    tableId, eventId)
                .Include(t => t.TableType)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Table not found for this event");

            if (table.Status == TableStatus.Booked)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("This table is already booked");
            }

            if (table.Status == TableStatus.Locked)
            {
                await transaction.RollbackAsync();
                if (table.LockedByUserId == userId)
                    throw new InvalidOperationException("You already hold this table");
                throw new InvalidOperationException("This table is currently held by another user");
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(holdMinutes);

            // Update table status
            table.Status = TableStatus.Locked;
            table.LockedByUserId = userId;
            table.LockExpiresAt = expiresAt;
            table.UpdatedAt = DateTime.UtcNow;

            // Also create SeatHolds for backward compatibility
            var seats = await context.Seats
                .Where(s => s.TableId == tableId)
                .OrderBy(s => s.SeatNumber)
                .ToListAsync();

            foreach (var seat in seats)
            {
                context.SeatHolds.Add(new SeatHold
                {
                    Id = Guid.NewGuid(),
                    SeatId = seat.Id,
                    EventId = eventId,
                    UserId = userId,
                    TicketTypeId = ticketTypeId,
                    ExpiresAt = expiresAt,
                    IsActive = true
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("[TableLock] User {UserId} locked table {TableLabel} for event {EventId}, expires {ExpiresAt}",
                userId, table.Label, eventId, expiresAt);

            return new TableLockDto(
                table.Id,
                table.Label,
                eventId,
                userId,
                "Locked",
                table.Capacity ?? 0,
                table.PriceType.ToString(),
                table.PriceOverrideCents ?? table.PriceCents,
                table.TableType?.PlatformFeeCents ?? 0,
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
        var table = await context.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId)
            ?? throw new KeyNotFoundException("Table not found");

        if (table.Status != TableStatus.Locked)
            throw new InvalidOperationException("Table is not locked");

        if (table.LockedByUserId != userId)
            throw new InvalidOperationException("You do not hold this table");

        table.Status = TableStatus.Available;
        table.LockedByUserId = null;
        table.LockExpiresAt = null;
        table.UpdatedAt = DateTime.UtcNow;

        // Release corresponding SeatHolds
        var seatIds = await context.Seats
            .Where(s => s.TableId == tableId)
            .Select(s => s.Id)
            .ToListAsync();

        await context.SeatHolds
            .Where(h => seatIds.Contains(h.SeatId) && h.EventId == eventId && h.UserId == userId && h.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.IsActive, false)
                .SetProperty(h => h.UpdatedAt, DateTime.UtcNow));

        await context.SaveChangesAsync();

        Log.Information("[TableLock] User {UserId} released table {TableLabel} for event {EventId}",
            userId, table.Label, eventId);
    }

    public async Task<List<TableLockDto>> GetUserLockedTablesAsync(Guid userId, Guid eventId)
    {
        var now = DateTime.UtcNow;
        return await context.Tables
            .Include(t => t.TableType)
            .Where(t => t.EventId == eventId
                && t.Status == TableStatus.Locked
                && t.LockedByUserId == userId
                && t.LockExpiresAt > now)
            .Select(t => new TableLockDto(
                t.Id,
                t.Label,
                eventId,
                userId,
                "Locked",
                t.Capacity ?? 0,
                t.PriceType.ToString(),
                t.PriceOverrideCents ?? t.PriceCents,
                t.TableType != null ? t.TableType.PlatformFeeCents : 0,
                t.LockExpiresAt!.Value
            ))
            .ToListAsync();
    }

    public async Task<int> CleanupExpiredLocksAsync()
    {
        var now = DateTime.UtcNow;

        // Release expired table locks
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

        // Expire pending bookings linked to tables that are no longer locked
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

        // Deactivate SeatHolds since the table is now permanently booked
        var seatIds = await context.Seats
            .Where(s => s.TableId == tableId)
            .Select(s => s.Id)
            .ToListAsync();

        if (seatIds.Count > 0)
        {
            await context.SeatHolds
                .Where(h => seatIds.Contains(h.SeatId) && h.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.IsActive, false)
                    .SetProperty(h => h.UpdatedAt, DateTime.UtcNow));
        }

        await context.SaveChangesAsync();

        Log.Information("[TableBooked] Table {TableId} marked as permanently booked", tableId);
    }
}
