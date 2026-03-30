using Contracts.DTOs.Seats;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

/// <summary>
/// Manages seat holds with PostgreSQL row-level locking (SELECT FOR UPDATE)
/// to prevent two users from holding the same seat simultaneously.
/// </summary>
public class SeatService(
    EventPlatformDbContext context,
    ISettingsService settings
) : ISeatService
{
    public async Task<VenueLayoutDto> GetLayoutAsync(Guid venueId, Guid? eventId = null)
    {
        var venue = await context.Venues.FindAsync(venueId)
            ?? throw new KeyNotFoundException("Venue not found");

        var tables = await context.Tables
            .Include(t => t.TableType)
            .Include(t => t.Seats).ThenInclude(s => s.Holds)
            .Where(t => t.VenueId == venueId)
            .OrderBy(t => t.Label)
            .ToListAsync();

        var now = DateTime.UtcNow;

        return new VenueLayoutDto(
            venue.Id,
            venue.Name,
            tables.Select(t => new TableDto(
                t.Id, t.Label, 0, 0, 0,
                t.TableType?.Name ?? "Unknown", t.TableType?.DefaultShape.ToString() ?? "Round",
                80, 80,
                t.PriceCents, t.TableType?.PlatformFeeCents ?? 0,
                t.Seats.OrderBy(s => s.SeatNumber).Select(s =>
                {
                    // A seat is held if there's an active, non-expired hold for this event
                    var activeHold = eventId.HasValue
                        ? s.Holds.FirstOrDefault(h => h.EventId == eventId.Value && h.IsActive && h.ExpiresAt > now)
                        : null;

                    return new SeatDto(
                        s.Id, s.Label, s.SeatNumber, t.Id, t.Label,
                        activeHold is not null,
                        activeHold?.UserId
                    );
                }).ToList()
            )).ToList()
        );
    }

    /// <summary>
    /// Hold a seat using PostgreSQL advisory lock + SELECT FOR UPDATE to prevent
    /// two users from holding the same seat simultaneously.
    /// </summary>
    public async Task<SeatHoldDto> HoldSeatAsync(Guid userId, Guid eventId, Guid seatId, Guid ticketTypeId)
    {
        var holdMinutes = int.Parse(
            await settings.GetOrDefaultAsync("hold_expiry_minutes", "10") ?? "10");

        // Verify seat and ticket type exist before entering transaction
        var seat = await context.Seats.Include(s => s.Table)
            .FirstOrDefaultAsync(s => s.Id == seatId)
            ?? throw new KeyNotFoundException("Seat not found");

        var ticketType = await context.TicketTypes.FindAsync(ticketTypeId)
            ?? throw new KeyNotFoundException("Ticket type not found");

        // Check if seat is already booked (Paid or CheckedIn)
        var isBooked = await context.BookingItems
            .AnyAsync(bi => bi.SeatId == seatId
                && bi.Booking.EventId == eventId
                && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn));
        if (isBooked)
            throw new InvalidOperationException("This seat is already booked");

        // Use a transaction with row-level locking via FOR UPDATE
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // SELECT FOR UPDATE locks any existing active hold row, blocking concurrent holds
            var existingHold = await context.SeatHolds
                .FromSqlRaw(
                    """
                    SELECT * FROM seat_holds
                    WHERE "SeatId" = {0} AND "EventId" = {1} AND "IsActive" = true AND "ExpiresAt" > NOW()
                    FOR UPDATE
                    """,
                    seatId, eventId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (existingHold is not null)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    existingHold.UserId == userId
                        ? "You already hold this seat"
                        : "This seat is currently held by another user");
            }

            // Double-check with a regular LINQ query in case no rows existed to lock
            var doubleCheck = await context.SeatHolds
                .AnyAsync(h => h.SeatId == seatId && h.EventId == eventId && h.IsActive && h.ExpiresAt > DateTime.UtcNow);

            if (doubleCheck)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("This seat is currently held by another user");
            }

            var hold = new SeatHold
            {
                Id = Guid.NewGuid(),
                SeatId = seatId,
                EventId = eventId,
                UserId = userId,
                TicketTypeId = ticketTypeId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(holdMinutes),
                IsActive = true
            };

            context.SeatHolds.Add(hold);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("[SeatHold] User {UserId} held seat {SeatLabel} for event {EventId}",
                userId, seat.Label, eventId);

            return new SeatHoldDto(
                hold.Id, seatId, seat.Label, eventId, userId,
                ticketTypeId, ticketType.Name ?? "", ticketType.PriceCents ?? 0, hold.ExpiresAt
            );
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception)
        {
            try { await transaction.RollbackAsync(); } catch { /* already rolled back */ }
            throw;
        }
    }

    public async Task ReleaseSeatAsync(Guid userId, Guid eventId, Guid seatId)
    {
        var hold = await context.SeatHolds
            .FirstOrDefaultAsync(h =>
                h.SeatId == seatId && h.EventId == eventId &&
                h.UserId == userId && h.IsActive);

        if (hold is null)
            throw new KeyNotFoundException("No active hold found for this seat");

        hold.IsActive = false;
        hold.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        Log.Information("[SeatHold] User {UserId} released seat {SeatId} for event {EventId}",
            userId, seatId, eventId);
    }

    public async Task<List<SeatHoldDto>> GetUserHoldsAsync(Guid userId, Guid eventId)
    {
        var now = DateTime.UtcNow;
        return await context.SeatHolds
            .Include(h => h.Seat)
            .Include(h => h.TicketType)
            .Where(h => h.UserId == userId && h.EventId == eventId && h.IsActive && h.ExpiresAt > now)
            .Select(h => new SeatHoldDto(
                h.Id, h.SeatId, h.Seat.Label, h.EventId, h.UserId,
                h.TicketTypeId, h.TicketType.Name ?? "", h.TicketType.PriceCents ?? 0, h.ExpiresAt
            ))
            .ToListAsync();
    }

    /// <summary>
    /// Hold all seats at a table atomically using SELECT FOR UPDATE.
    /// If any seat is already held by another user, the entire hold is rejected.
    /// </summary>
    public async Task<List<SeatHoldDto>> HoldTableAsync(Guid userId, Guid eventId, Guid tableId, Guid ticketTypeId)
    {
        var holdMinutes = int.Parse(
            await settings.GetOrDefaultAsync("hold_expiry_minutes", "10") ?? "10");

        var table = await context.Tables.Include(t => t.Seats)
            .FirstOrDefaultAsync(t => t.Id == tableId)
            ?? throw new KeyNotFoundException("Table not found");

        if (table.Seats.Count == 0)
            throw new InvalidOperationException("Table has no seats configured");

        var ticketType = await context.TicketTypes.FindAsync(ticketTypeId)
            ?? throw new KeyNotFoundException("Ticket type not found");

        // Check if any seat in this table is already booked
        var tableSeatIds = table.Seats.Select(s => s.Id).ToList();
        var anyBooked = await context.BookingItems
            .AnyAsync(bi => bi.SeatId.HasValue
                && tableSeatIds.Contains(bi.SeatId.Value)
                && bi.Booking.EventId == eventId
                && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn));
        if (anyBooked)
            throw new InvalidOperationException("This table is already booked");

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var seatIds = table.Seats.Select(s => s.Id).ToList();

            // Lock all existing active holds for seats at this table using parameterized query
            var existingHolds = new List<SeatHold>();
            foreach (var seatId in seatIds)
            {
                var held = await context.SeatHolds
                    .FromSqlRaw(
                        """
                        SELECT * FROM seat_holds
                        WHERE "SeatId" = {0} AND "EventId" = {1} AND "IsActive" = true AND "ExpiresAt" > NOW()
                        FOR UPDATE
                        """,
                        seatId, eventId)
                    .AsNoTracking()
                    .ToListAsync();
                existingHolds.AddRange(held);
            }

            if (existingHolds.Count > 0)
            {
                var heldByMe = existingHolds.All(h => h.UserId == userId);
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    heldByMe ? "You already hold this table" : "This table is currently held by another user");
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(holdMinutes);
            var holds = new List<SeatHold>();

            foreach (var seat in table.Seats.OrderBy(s => s.SeatNumber))
            {
                var hold = new SeatHold
                {
                    Id = Guid.NewGuid(),
                    SeatId = seat.Id,
                    EventId = eventId,
                    UserId = userId,
                    TicketTypeId = ticketTypeId,
                    ExpiresAt = expiresAt,
                    IsActive = true
                };
                context.SeatHolds.Add(hold);
                holds.Add(hold);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("[TableHold] User {UserId} held table {TableLabel} ({SeatCount} seats) for event {EventId}",
                userId, table.Label, table.Seats.Count, eventId);

            return holds.Select(h =>
            {
                var seat = table.Seats.First(s => s.Id == h.SeatId);
                return new SeatHoldDto(h.Id, h.SeatId, seat.Label, eventId, userId,
                    ticketTypeId, ticketType.Name ?? "", ticketType.PriceCents ?? 0, h.ExpiresAt);
            }).ToList();
        }
        catch (InvalidOperationException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch (Exception)
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    public async Task ReleaseTableAsync(Guid userId, Guid eventId, Guid tableId)
    {
        var seatIds = await context.Seats
            .Where(s => s.TableId == tableId)
            .Select(s => s.Id)
            .ToListAsync();

        var holds = await context.SeatHolds
            .Where(h => seatIds.Contains(h.SeatId) && h.EventId == eventId && h.UserId == userId && h.IsActive)
            .ToListAsync();

        foreach (var h in holds)
        {
            h.IsActive = false;
            h.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        Log.Information("[TableHold] User {UserId} released table {TableId} ({Count} seats) for event {EventId}",
            userId, tableId, holds.Count, eventId);
    }

    public async Task<int> CleanupExpiredHoldsAsync()
    {
        var now = DateTime.UtcNow;
        return await context.SeatHolds
            .Where(h => h.IsActive && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.IsActive, false)
                .SetProperty(h => h.UpdatedAt, now));
    }
}
