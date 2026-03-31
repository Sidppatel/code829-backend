using System.Security.Cryptography;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds bookings across events and users. Grid events get table-based bookings
/// (one BookingItem per seat with SeatId). Non-Grid events get standard ticket bookings.
/// Distribution: 60% Paid, 15% Pending, 15% CheckedIn, 5% Cancelled, 5% Refunded.
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
            .Include(e => e.TicketTypes)
            .Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.Completed)
            .ToListAsync();

        if (users.Count == 0 || events.Count == 0)
            return;

        var gridEvents = events.Where(e => e.LayoutMode == LayoutMode.Grid).ToList();
        var nonGridEvents = events.Where(e => e.LayoutMode != LayoutMode.Grid).ToList();

        var rng = new Random(42);
        var bookingNumber = 1;

        // ── Grid event bookings (table-based) ────────────────────────
        foreach (var ev in gridEvents)
        {
            var tables = await context.Tables
                .Include(t => t.Seats)
                .Where(t => t.EventId == ev.Id && t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            if (tables.Count == 0 || ev.TicketTypes.Count == 0) continue;

            var ticketType = ev.TicketTypes.First();
            // Book 30-60% of tables
            var tablesToBook = tables.OrderBy(_ => rng.Next())
                .Take(Math.Max(1, (int)(tables.Count * (0.3 + rng.NextDouble() * 0.3))))
                .ToList();

            foreach (var table in tablesToBook)
            {
                if (table.Seats.Count == 0) continue;

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
                    SubtotalCents = tablePrice,
                    FeeCents = fee,
                    TotalCents = tablePrice + fee,
                    QrToken = qrToken,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                context.Bookings.Add(booking);

                // One BookingItem per seat at the table
                foreach (var seat in table.Seats.OrderBy(s => s.SeatNumber))
                {
                    var itemQr = (status is BookingStatus.Paid or BookingStatus.CheckedIn) ? GenerateQrToken() : null;
                    var invToken = (status is BookingStatus.Paid or BookingStatus.CheckedIn) ? GenerateInvitationToken() : null;

                    context.BookingItems.Add(new BookingItem
                    {
                        Id = Guid.NewGuid(),
                        BookingId = booking.Id,
                        TicketTypeId = ticketType.Id,
                        SeatId = seat.Id,
                        PriceCents = 0, // price is on the table, not per-seat
                        QrToken = itemQr,
                        InvitationToken = invToken,
                        CreatedAt = createdAt,
                        UpdatedAt = createdAt
                    });
                }

                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    ticketType.QuantitySold += table.Seats.Count;

                AddPayment(context, booking, status, createdAt);
            }
        }

        // ── Non-Grid event bookings (ticket-based) ───────────────────
        var nonGridBookingTarget = 45;
        for (var i = 0; i < nonGridBookingTarget; i++)
        {
            var ev = nonGridEvents[rng.Next(nonGridEvents.Count)];
            if (ev.TicketTypes.Count == 0) continue;

            var user = users[rng.Next(users.Count)];
            var status = PickStatus(rng);
            var createdAt = DateTime.UtcNow.AddDays(-rng.Next(1, 30)).AddHours(-rng.Next(0, 24));

            var itemCount = rng.Next(1, Math.Min(4, ev.TicketTypes.Count + 1));
            var selectedTickets = ev.TicketTypes.OrderBy(_ => rng.Next()).Take(itemCount).ToList();

            var subtotal = selectedTickets.Sum(tt => tt.PriceCents ?? 0);
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
                SubtotalCents = subtotal,
                FeeCents = fee,
                TotalCents = subtotal + fee,
                QrToken = qrToken,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
            context.Bookings.Add(booking);

            foreach (var tt in selectedTickets)
            {
                var itemQr = (status is BookingStatus.Paid or BookingStatus.CheckedIn) ? GenerateQrToken() : null;

                context.BookingItems.Add(new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    TicketTypeId = tt.Id,
                    PriceCents = tt.PriceCents ?? 0,
                    QrToken = itemQr,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });

                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                    tt.QuantitySold++;
            }

            AddPayment(context, booking, status, createdAt);
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

    private static string GenerateInvitationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
