using System.Security.Cryptography;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds bookings across events and users on a fresh DB.
///
/// Lifecycle produced (matches the runtime flow):
///   Pending     — booking created, no tickets yet.
///   Paid        — sp_confirm_booking ran → tickets created in Unassigned; a realistic mix
///                 is then patched to Invited / Claimed / Unassigned so the demo UI shows
///                 the full state machine. Check-in will reject anything not Claimed.
///   CheckedIn   — all tickets Claimed first (ClaimedAt set), then marked CheckedIn.
///   Cancelled / Refunded — confirmed + then transitioned, so tickets exist but are stale.
///
/// Distribution target: ~60% Paid, ~15% CheckedIn, ~15% Pending, ~5% Cancelled, ~5% Refunded.
/// </summary>
public static class BookingSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var bookingProc = scope.ServiceProvider.GetRequiredService<IBookingProcedures>();
        var stripeTransactionProc = scope.ServiceProvider.GetRequiredService<IStripeTransactionProcedures>();
        var tableProc = scope.ServiceProvider.GetRequiredService<ITableProcedures>();

        if (await context.Bookings.AnyAsync())
            return;

        var users = await context.Users.ToListAsync();
        var events = await context.Events
            .Where(e => e.Status == EventStatus.Published)
            .ToListAsync();

        if (users.Count == 0 || events.Count == 0)
            return;

        var gridEvents = events.Where(e => e.LayoutMode == LayoutMode.Grid).ToList();
        var openEvents = events.Where(e => e.LayoutMode == LayoutMode.Open).ToList();

        var rng = new Random(42);
        var bookingNumber = 1;

        foreach (var ev in gridEvents)
        {
            await SeedTableBookingsAsync(context, bookingProc, stripeTransactionProc, tableProc, ev, users, rng, () => bookingNumber++);
        }

        foreach (var ev in openEvents)
        {
            await SeedOpenBookingsAsync(context, bookingProc, stripeTransactionProc, ev, users, rng, () => bookingNumber++);
        }

        var seedTotal = await context.Bookings.CountAsync();
        Log.Information("[Seed] Created {Total} bookings via SP", seedTotal);
    }

    private static async Task SeedTableBookingsAsync(
        EventPlatformDbContext context,
        IBookingProcedures bookingProc,
        IStripeTransactionProcedures stripeTransactionProc,
        ITableProcedures tableProc,
        Event ev,
        List<User> users,
        Random rng,
        Func<int> nextBookingNumber)
    {
        var tables = await context.Tables
            .Include(t => t.EventTable)
            .Where(t => t.EventId == ev.Id && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        if (tables.Count == 0) return;

        var tablesToBook = tables.OrderBy(_ => rng.Next())
            .Take(Math.Max(1, (int)(tables.Count * (0.3 + rng.NextDouble() * 0.3))))
            .ToList();

        foreach (var table in tablesToBook)
        {
            var user = users[rng.Next(users.Count)];
            var desiredStatus = PickStatus(rng);
            var tablePrice = table.EventTable.PriceCents;
            var fee = table.EventTable.PlatformFeeCents ?? 0;
            var total = tablePrice + fee;
            var bn = $"BK-SEED-{nextBookingNumber():D4}";

            // Always create as Pending, then transition — mirrors the live booking flow.
            var bookingId = await bookingProc.CreateBookingAsync(
                user.Id, ev.Id, table.Id, null, null,
                tablePrice, fee, total, bn);

            await ApplyBookingStateAsync(context, bookingProc, tableProc, bookingId, desiredStatus, users, table.Id, rng);
            await AddStripeTransactionAsync(stripeTransactionProc, bookingId, total, tablePrice, desiredStatus);
        }
    }

    private static async Task SeedOpenBookingsAsync(
        EventPlatformDbContext context,
        IBookingProcedures bookingProc,
        IStripeTransactionProcedures stripeTransactionProc,
        Event ev,
        List<User> users,
        Random rng,
        Func<int> nextBookingNumber)
    {
        var ticketTypes = await context.EventTicketTypes
            .Where(tt => tt.EventId == ev.Id && tt.IsActive)
            .ToListAsync();

        if (ticketTypes.Count == 0) return;

        var maxCap = ev.MaxCapacity ?? 200;
        var totalSeatsBooked = 0;
        var bookingCount = ev.IsFeatured ? rng.Next(25, 45) : rng.Next(10, 25);

        for (var i = 0; i < bookingCount; i++)
        {
            var seatsReserved = rng.Next(1, 5);
            if (totalSeatsBooked + seatsReserved > maxCap * 0.85)
                break;

            var user = users[rng.Next(users.Count)];
            var desiredStatus = PickStatus(rng, ev.IsFeatured);
            var selectedType = ticketTypes[rng.Next(ticketTypes.Count)];
            var pricePerPerson = selectedType.PriceCents;
            var subtotal = pricePerPerson * seatsReserved;
            var fee = (selectedType.PlatformFeeCents ?? 0) * seatsReserved;
            var total = subtotal + fee;
            var bn = $"BK-SEED-{nextBookingNumber():D4}";

            var bookingId = await bookingProc.CreateBookingAsync(
                user.Id, ev.Id, null, seatsReserved, selectedType.Id,
                subtotal, fee, total, bn);

            await ApplyBookingStateAsync(context, bookingProc, tableProc: null, bookingId, desiredStatus, users, tableId: null, rng);

            if (desiredStatus is BookingStatus.Paid or BookingStatus.CheckedIn)
                totalSeatsBooked += seatsReserved;

            await AddStripeTransactionAsync(stripeTransactionProc, bookingId, total, subtotal, desiredStatus);
        }
    }

    // Drives the booking from Pending to whichever final state we're seeding, plus fills the
    // resulting tickets with a realistic spread of Unassigned/Invited/Claimed/CheckedIn.
    private static async Task ApplyBookingStateAsync(
        EventPlatformDbContext context,
        IBookingProcedures bookingProc,
        ITableProcedures? tableProc,
        Guid bookingId,
        BookingStatus target,
        List<User> users,
        Guid? tableId,
        Random rng)
    {
        if (target == BookingStatus.Pending)
            return; // No tickets, no table lock, nothing more to do.

        // Confirm first — sp_confirm_booking is the only path that creates booking_tickets.
        var qrToken = GenerateQrToken();
        await bookingProc.ConfirmBookingAsync(bookingId, qrToken);

        if (tableId.HasValue && tableProc is not null && target is BookingStatus.Paid or BookingStatus.CheckedIn)
            await tableProc.MarkTableBookedAsync(tableId.Value);

        var tickets = await context.BookingTickets
            .Where(t => t.BookingId == bookingId)
            .ToListAsync();
        if (tickets.Count == 0)
            return;

        var booking = await context.Bookings.FirstAsync(b => b.Id == bookingId);

        switch (target)
        {
            case BookingStatus.CheckedIn:
                // Every ticket must be Claimed before CheckedIn (new rule). Give each a claimer
                // and set both timestamps in the past for realism.
                foreach (var ticket in tickets)
                {
                    var claimer = users[rng.Next(users.Count)];
                    ticket.GuestUserId = claimer.Id;
                    ticket.ClaimedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 3));
                    ticket.Status = TicketStatus.CheckedIn;
                    ticket.InviteTokenHash = null;
                    ticket.UpdatedAt = DateTime.UtcNow;
                }
                booking.Status = BookingStatus.CheckedIn;
                booking.UpdatedAt = DateTime.UtcNow;
                break;

            case BookingStatus.Paid:
                // Natural mix so the demo UI exercises every ticket-action branch.
                foreach (var ticket in tickets)
                {
                    var roll = rng.NextDouble();
                    if (roll < 0.40)
                        AssignClaimed(ticket, users[rng.Next(users.Count)], rng);
                    else if (roll < 0.70)
                        AssignInvited(ticket);
                    // else keep Unassigned as produced by sp_confirm_booking.
                }
                break;

            case BookingStatus.Cancelled:
                booking.Status = BookingStatus.Cancelled;
                booking.UpdatedAt = DateTime.UtcNow;
                break;

            case BookingStatus.Refunded:
                booking.Status = BookingStatus.Refunded;
                booking.UpdatedAt = DateTime.UtcNow;
                break;
        }

        await context.SaveChangesAsync();
    }

    private static void AssignClaimed(BookingTicket ticket, User claimer, Random rng)
    {
        ticket.GuestUserId = claimer.Id;
        ticket.ClaimedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 72));
        ticket.Status = TicketStatus.Claimed;
        ticket.InviteTokenHash = null;
        ticket.UpdatedAt = DateTime.UtcNow;
    }

    private static void AssignInvited(BookingTicket ticket)
    {
        // Token hash is a throwaway SHA-256 of a random blob — the seed flow never hands the
        // raw token anywhere, so nobody can actually claim these; they just render as "Invited"
        // in the UI until someone uses the live invite flow on a real booking.
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        ticket.InviteTokenHash = hash;
        ticket.InviteExpiresAt = DateTime.UtcNow.AddDays(7);
        ticket.InvitedEmail = $"guest{Random.Shared.Next(1000, 9999)}@example.com";
        ticket.InviteSentAt = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(5, 120));
        ticket.Status = TicketStatus.Invited;
        ticket.UpdatedAt = DateTime.UtcNow;
    }

    private static BookingStatus PickStatus(Random rng, bool isFeatured = false)
    {
        var roll = rng.NextDouble();
        if (isFeatured)
        {
            return roll switch
            {
                < 0.60 => BookingStatus.Paid,
                < 0.75 => BookingStatus.CheckedIn,
                < 0.90 => BookingStatus.Pending,
                < 0.95 => BookingStatus.Cancelled,
                _ => BookingStatus.Refunded
            };
        }

        return roll switch
        {
            < 0.60 => BookingStatus.Paid,
            < 0.75 => BookingStatus.CheckedIn,
            < 0.90 => BookingStatus.Pending,
            < 0.95 => BookingStatus.Cancelled,
            _ => BookingStatus.Refunded
        };
    }

    private static async Task AddStripeTransactionAsync(
        IStripeTransactionProcedures stripeTransactionProc, Guid bookingId,
        int amountCents, int transferAmountCents, BookingStatus status)
    {
        var paymentStatus = status switch
        {
            BookingStatus.Paid or BookingStatus.CheckedIn => "Succeeded",
            BookingStatus.Refunded => "Refunded",
            BookingStatus.Cancelled => "Failed",
            _ => "RequiresConfirmation"
        };

        var intentId = $"pi_seed_{Guid.NewGuid():N}";
        await stripeTransactionProc.CreateAsync(bookingId, intentId, amountCents, transferAmountCents);

        if (paymentStatus != "RequiresConfirmation")
            await stripeTransactionProc.UpdateStatusAsync(intentId, paymentStatus);
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
