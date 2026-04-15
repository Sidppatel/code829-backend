using System.Security.Cryptography;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds bookings across events and users via stored procedures.
/// Grid events: table bookings (TableId set, table marked as Booked).
/// Open events: capacity bookings (SeatsReserved set).
/// Distribution: ~60% Paid, ~15% Pending, ~15% CheckedIn, ~5% Cancelled, ~5% Refunded.
/// </summary>
public static class BookingSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var bookingProc = scope.ServiceProvider.GetRequiredService<IBookingProcedures>();
        var paymentProc = scope.ServiceProvider.GetRequiredService<IPaymentProcedures>();
        var tableProc = scope.ServiceProvider.GetRequiredService<ITableProcedures>();

        if (await context.Bookings.AnyAsync())
            return;

        var users = await context.Users
            .ToListAsync();

        var events = await context.Events
            .Where(e => e.Status == EventStatus.Published)
            .ToListAsync();

        if (users.Count == 0 || events.Count == 0)
            return;

        var gridEvents = events.Where(e => e.LayoutMode == LayoutMode.Grid).ToList();
        var openEvents = events.Where(e => e.LayoutMode == LayoutMode.Open).ToList();

        var rng = new Random(42);
        var bookingNumber = 1;

        // Grid event bookings (table-based)
        foreach (var ev in gridEvents)
        {
            var tables = await context.Tables
                .Include(t => t.EventTable)
                .Where(t => t.EventId == ev.Id && t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            if (tables.Count == 0) continue;

            var tablesToBook = tables.OrderBy(_ => rng.Next())
                .Take(Math.Max(1, (int)(tables.Count * (0.3 + rng.NextDouble() * 0.3))))
                .ToList();

            foreach (var table in tablesToBook)
            {
                var user = users[rng.Next(users.Count)];
                var status = PickStatus(rng);
                var tablePrice = table.EventTable.PriceCents;
                var fee = table.EventTable.PlatformFeeCents ?? 0;
                var bn = $"BK-SEED-{bookingNumber++:D4}";

                var bookingId = await bookingProc.CreateBookingAsync(
                    user.Id, ev.Id, table.Id, null, null,
                    tablePrice, fee, tablePrice + fee, bn, status.ToString());

                // Mark table as booked for confirmed bookings
                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    await tableProc.MarkTableBookedAsync(table.Id);

                await AddPaymentAsync(paymentProc, bookingId, status);
            }
        }

        // Open event bookings (capacity-based, with ticket types)
        foreach (var ev in openEvents)
        {
            var ticketTypes = await context.EventTicketTypes
                .Where(tt => tt.EventId == ev.Id && tt.IsActive)
                .ToListAsync();

            if (ticketTypes.Count == 0) continue;

            var maxCap = ev.MaxCapacity ?? 200;
            var totalSeatsBooked = 0;

            // More bookings for featured events
            var bookingCount = ev.IsFeatured ? rng.Next(25, 45) : rng.Next(10, 25);
            
            for (var i = 0; i < bookingCount; i++)
            {
                var seatsReserved = rng.Next(1, 5);
                if (totalSeatsBooked + seatsReserved > maxCap * 0.85)
                    break;

                var user = users[rng.Next(users.Count)];
                var status = PickStatus(rng, ev.IsFeatured);

                var selectedType = ticketTypes[rng.Next(ticketTypes.Count)];
                var pricePerPerson = selectedType.PriceCents;
                var ticketTypeId = selectedType.Id;

                var subtotal = pricePerPerson * seatsReserved;
                var fee = (selectedType.PlatformFeeCents ?? 0) * seatsReserved;
                var bn = $"BK-SEED-{bookingNumber++:D4}";

                var bookingId = await bookingProc.CreateBookingAsync(
                    user.Id, ev.Id, null, seatsReserved, ticketTypeId,
                    subtotal, fee, subtotal + fee, bn, status.ToString());

                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    totalSeatsBooked += seatsReserved;

                await AddPaymentAsync(paymentProc, bookingId, status);
            }
        }

        var total = await context.Bookings.CountAsync();
        Log.Information("[Seed] Created {Total} bookings via SP", total);
    }

    private static BookingStatus PickStatus(Random rng, bool isFeatured = false)
    {
        var roll = rng.NextDouble();
        // Featured events have more successful payments
        if (isFeatured)
        {
            return roll switch
            {
                < 0.75 => BookingStatus.Paid,
                < 0.85 => BookingStatus.Pending,
                < 0.95 => BookingStatus.CheckedIn,
                < 0.98 => BookingStatus.Cancelled,
                _ => BookingStatus.Refunded
            };
        }

        return roll switch
        {
            < 0.60 => BookingStatus.Paid,
            < 0.75 => BookingStatus.Pending,
            < 0.90 => BookingStatus.CheckedIn,
            < 0.95 => BookingStatus.Cancelled,
            _ => BookingStatus.Refunded
        };
    }

    private static async Task AddPaymentAsync(IPaymentProcedures paymentProc, Guid bookingId, BookingStatus status)
    {
        var paymentStatus = status switch
        {
            BookingStatus.Paid or BookingStatus.CheckedIn => "Succeeded",
            BookingStatus.Refunded => "Refunded",
            BookingStatus.Cancelled => "Failed",
            _ => "RequiresConfirmation"
        };

        var intentId = $"pi_seed_{Guid.NewGuid():N}";
        await paymentProc.CreatePaymentAsync(bookingId, intentId, 0);

        if (paymentStatus != "RequiresConfirmation")
            await paymentProc.UpdatePaymentStatusAsync(intentId, paymentStatus);
    }
}
