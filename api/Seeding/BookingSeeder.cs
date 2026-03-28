using System.Security.Cryptography;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds 65 bookings across events and users with distribution:
/// 60% Paid, 15% Pending, 15% CheckedIn, 5% Cancelled, 5% Refunded.
/// QR tokens generated for Paid and CheckedIn bookings.
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

        var rng = new Random(42); // Deterministic seed for reproducibility
        var bookingCount = 65;
        var bookingNumber = 1;

        // Status distribution: 60% Paid, 15% Pending, 15% CheckedIn, 5% Cancelled, 5% Refunded
        var statuses = new List<BookingStatus>();
        statuses.AddRange(Enumerable.Repeat(BookingStatus.Paid, (int)(bookingCount * 0.60)));
        statuses.AddRange(Enumerable.Repeat(BookingStatus.Pending, (int)(bookingCount * 0.15)));
        statuses.AddRange(Enumerable.Repeat(BookingStatus.CheckedIn, (int)(bookingCount * 0.15)));
        statuses.AddRange(Enumerable.Repeat(BookingStatus.Cancelled, (int)(bookingCount * 0.05)));
        statuses.AddRange(Enumerable.Repeat(BookingStatus.Refunded, (int)(bookingCount * 0.05)));
        // Fill remaining to reach exact count
        while (statuses.Count < bookingCount)
            statuses.Add(BookingStatus.Paid);

        // Shuffle
        for (int i = statuses.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (statuses[i], statuses[j]) = (statuses[j], statuses[i]);
        }

        for (int i = 0; i < bookingCount; i++)
        {
            var user = users[rng.Next(users.Count)];
            var ev = events[rng.Next(events.Count)];
            var status = statuses[i];

            if (ev.TicketTypes.Count == 0)
                continue;

            // Pick 1-3 items per booking
            var itemCount = rng.Next(1, Math.Min(4, ev.TicketTypes.Count + 1));
            var selectedTicketTypes = ev.TicketTypes.OrderBy(_ => rng.Next()).Take(itemCount).ToList();

            var subtotal = selectedTicketTypes.Sum(tt => tt.PriceCents);
            var fee = (int)Math.Ceiling(subtotal * 0.08); // 8% platform fee
            var total = subtotal + fee;

            string? qrToken = null;
            if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
            {
                var bytes = RandomNumberGenerator.GetBytes(24);
                qrToken = $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
            }

            var createdAt = DateTime.UtcNow.AddDays(-rng.Next(1, 30)).AddHours(-rng.Next(0, 24));
            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                BookingNumber = $"BK-SEED-{bookingNumber++:D4}",
                Status = status,
                UserId = user.Id,
                EventId = ev.Id,
                SubtotalCents = subtotal,
                FeeCents = fee,
                TotalCents = total,
                QrToken = qrToken,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            context.Bookings.Add(booking);

            foreach (var tt in selectedTicketTypes)
            {
                context.BookingItems.Add(new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    TicketTypeId = tt.Id,
                    PriceCents = tt.PriceCents,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });

                // Increment sold count for Paid/CheckedIn
                if (status is BookingStatus.Paid or BookingStatus.CheckedIn)
                {
                    tt.QuantitySold++;
                }
            }

            // Create payment record
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
                AmountCents = total,
                PaidAt = paymentStatus == PaymentStatus.Succeeded ? createdAt.AddMinutes(1) : null,
                RefundedAt = paymentStatus == PaymentStatus.Refunded ? createdAt.AddDays(1) : null,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        }

        await context.SaveChangesAsync();

        var counts = await context.Bookings.GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var summary = string.Join(", ", counts.Select(c => $"{c.Status}: {c.Count}"));
        Log.Information("[Seed] Created {Total} bookings ({Summary})", bookingCount, summary);
    }
}
