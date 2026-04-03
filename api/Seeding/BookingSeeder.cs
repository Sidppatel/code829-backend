using System.Security.Cryptography;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds bookings across events and users.
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

        if (await context.Bookings.AnyAsync())
            return;

        var users = await context.Users
            .Where(u => u.Role == UserRole.User)
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

        // ── Grid event bookings (table-based) ────────────────────────
        foreach (var ev in gridEvents)
        {
            var tables = await context.Tables
                .Where(t => t.EventId == ev.Id && t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            if (tables.Count == 0) continue;

            // Book 30-60% of tables
            var tablesToBook = tables.OrderBy(_ => rng.Next())
                .Take(Math.Max(1, (int)(tables.Count * (0.3 + rng.NextDouble() * 0.3))))
                .ToList();

            foreach (var table in tablesToBook)
            {
                var user = users[rng.Next(users.Count)];
                var status = PickStatus(rng);
                var createdAt = DateTime.UtcNow.AddDays(-rng.Next(1, 20)).AddHours(-rng.Next(0, 24));
                var tablePrice = table.PriceCents;
                var fee = (int)Math.Ceiling(tablePrice * 0.08);

                string? qrToken = null;
                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    qrToken = GenerateQrToken();

                var booking = new Booking
                {
                    Id = Guid.NewGuid(),
                    BookingNumber = $"BK-SEED-{bookingNumber++:D4}",
                    Status = status,
                    UserId = user.Id,
                    EventId = ev.Id,
                    TableId = table.Id,
                    SubtotalCents = tablePrice,
                    FeeCents = fee,
                    TotalCents = tablePrice + fee,
                    QrToken = qrToken,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                context.Bookings.Add(booking);

                // Mark the table as booked for confirmed bookings
                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                {
                    table.Status = TableStatus.Booked;
                }

                AddPayment(context, booking, status, createdAt);
            }
        }

        // ── Open event bookings (capacity-based) ─────────────────────
        foreach (var ev in openEvents)
        {
            var pricePerPerson = ev.PricePerPersonCents ?? 0;
            var maxCap = ev.MaxCapacity ?? 200;
            var totalSeatsBooked = 0;

            // Create 5-12 bookings per open event
            var bookingCount = rng.Next(5, 13);
            for (var i = 0; i < bookingCount; i++)
            {
                var seatsReserved = rng.Next(1, 7); // 1-6 seats per booking
                if (totalSeatsBooked + seatsReserved > maxCap * 0.6)
                    break;

                var user = users[rng.Next(users.Count)];
                var status = PickStatus(rng);
                var createdAt = DateTime.UtcNow.AddDays(-rng.Next(1, 30)).AddHours(-rng.Next(0, 24));

                var subtotal = pricePerPerson * seatsReserved;
                var fee = (int)Math.Ceiling(subtotal * 0.08);

                string? qrToken = null;
                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    qrToken = GenerateQrToken();

                var booking = new Booking
                {
                    Id = Guid.NewGuid(),
                    BookingNumber = $"BK-SEED-{bookingNumber++:D4}",
                    Status = status,
                    UserId = user.Id,
                    EventId = ev.Id,
                    SeatsReserved = seatsReserved,
                    SubtotalCents = subtotal,
                    FeeCents = fee,
                    TotalCents = subtotal + fee,
                    QrToken = qrToken,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                context.Bookings.Add(booking);

                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    totalSeatsBooked += seatsReserved;

                AddPayment(context, booking, status, createdAt);
            }
        }

        await context.SaveChangesAsync();

        var total = await context.Bookings.CountAsync();
        var counts = await context.Bookings.GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var summary = string.Join(", ", counts.Select(c => $"{c.Status}: {c.Count}"));
        Log.Information("[Seed] Created {Total} bookings ({Summary})", total, summary);
    }

    private static BookingStatus PickStatus(Random rng)
    {
        var roll = rng.NextDouble();
        return roll switch
        {
            < 0.60 => BookingStatus.Paid,
            < 0.75 => BookingStatus.Pending,
            < 0.90 => BookingStatus.CheckedIn,
            < 0.95 => BookingStatus.Cancelled,
            _ => BookingStatus.Refunded
        };
    }

    private static void AddPayment(EventPlatformDbContext context, Booking booking, BookingStatus status, DateTime createdAt)
    {
        var paymentStatus = status switch
        {
            BookingStatus.Paid or BookingStatus.CheckedIn => PaymentStatus.Succeeded,
            BookingStatus.Refunded => PaymentStatus.Refunded,
            BookingStatus.Cancelled => PaymentStatus.Failed,
            _ => PaymentStatus.RequiresConfirmation
        };

        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            PaymentIntentId = $"pi_seed_{Guid.NewGuid():N}",
            Status = paymentStatus,
            AmountCents = booking.TotalCents,
            PaidAt = paymentStatus is PaymentStatus.Succeeded or PaymentStatus.Refunded
                ? createdAt.AddMinutes(1) : null,
            RefundedAt = paymentStatus == PaymentStatus.Refunded ? createdAt.AddDays(1) : null,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
