using System.Security.Cryptography;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds purchases across events and users on a fresh DB.
///
/// Lifecycle produced (matches the runtime flow):
///   Pending     — purchase created, no tickets yet.
///   Paid        — sp_confirm_purchase ran → tickets created in Unassigned; a realistic mix
///                 is then patched to Invited / Claimed / Unassigned so the demo UI shows
///                 the full state machine. Check-in will reject anything not Claimed.
///   CheckedIn   — all tickets Claimed first (ClaimedAt set), then marked CheckedIn.
///   Cancelled / Refunded — confirmed + then transitioned, so tickets exist but are stale.
///
/// Distribution target: ~60% Paid, ~15% CheckedIn, ~15% Pending, ~5% Cancelled, ~5% Refunded.
/// </summary>
public static class PurchaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var purchaseProc = scope.ServiceProvider.GetRequiredService<IPurchaseProcedures>();
        var stripeTransactionProc = scope.ServiceProvider.GetRequiredService<IStripeTransactionProcedures>();
        var tableProc = scope.ServiceProvider.GetRequiredService<ITableProcedures>();

        if (await context.Purchases.AnyAsync())
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
        var purchaseNumber = 1;

        foreach (var ev in gridEvents)
        {
            await SeedTablePurchasesAsync(context, purchaseProc, stripeTransactionProc, tableProc, ev, users, rng, () => purchaseNumber++);
        }

        foreach (var ev in openEvents)
        {
            await SeedOpenPurchasesAsync(context, purchaseProc, stripeTransactionProc, ev, users, rng, () => purchaseNumber++);
        }

        var seedTotal = await context.Purchases.CountAsync();
        Log.Information("[Seed] Created {Total} purchases via SP", seedTotal);
    }

    private static async Task SeedTablePurchasesAsync(
        EventPlatformDbContext context,
        IPurchaseProcedures purchaseProc,
        IStripeTransactionProcedures stripeTransactionProc,
        ITableProcedures tableProc,
        Event ev,
        List<User> users,
        Random rng,
        Func<int> nextPurchaseNumber)
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
            var bn = $"BK-SEED-{nextPurchaseNumber():D4}";

            // Always create as Pending, then transition — mirrors the live purchase flow.
            var purchaseId = await purchaseProc.CreatePurchaseAsync(
                user.Id, ev.Id, table.Id, null, null,
                tablePrice, fee, total, bn);

            await ApplyPurchaseStateAsync(context, purchaseProc, tableProc, purchaseId, desiredStatus, users, table.Id, rng);
            await AddStripeTransactionAsync(stripeTransactionProc, purchaseId, total, tablePrice, desiredStatus);
        }
    }

    private static async Task SeedOpenPurchasesAsync(
        EventPlatformDbContext context,
        IPurchaseProcedures purchaseProc,
        IStripeTransactionProcedures stripeTransactionProc,
        Event ev,
        List<User> users,
        Random rng,
        Func<int> nextPurchaseNumber)
    {
        var ticketTypes = await context.EventTicketTypes
            .Where(tt => tt.EventId == ev.Id && tt.IsActive)
            .ToListAsync();

        if (ticketTypes.Count == 0) return;

        var maxCap = ev.MaxCapacity ?? 200;
        var totalSeatsBooked = 0;
        var purchaseCount = ev.IsFeatured ? rng.Next(25, 45) : rng.Next(10, 25);

        for (var i = 0; i < purchaseCount; i++)
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
            var bn = $"BK-SEED-{nextPurchaseNumber():D4}";

            var purchaseId = await purchaseProc.CreatePurchaseAsync(
                user.Id, ev.Id, null, seatsReserved, selectedType.Id,
                subtotal, fee, total, bn);

            await ApplyPurchaseStateAsync(context, purchaseProc, tableProc: null, purchaseId, desiredStatus, users, tableId: null, rng);

            if (desiredStatus is PurchaseStatus.Paid or PurchaseStatus.CheckedIn)
                totalSeatsBooked += seatsReserved;

            await AddStripeTransactionAsync(stripeTransactionProc, purchaseId, total, subtotal, desiredStatus);
        }
    }

    // Drives the purchase from Pending to whichever final state we're seeding, plus fills the
    // resulting tickets with a realistic spread of Unassigned/Invited/Claimed/CheckedIn.
    private static async Task ApplyPurchaseStateAsync(
        EventPlatformDbContext context,
        IPurchaseProcedures purchaseProc,
        ITableProcedures? tableProc,
        Guid purchaseId,
        PurchaseStatus target,
        List<User> users,
        Guid? tableId,
        Random rng)
    {
        if (target == PurchaseStatus.Pending)
            return; // No tickets, no table lock, nothing more to do.

        // Confirm first — sp_confirm_purchase is the only path that creates purchase_tickets.
        var qrToken = GenerateQrToken();
        await purchaseProc.ConfirmPurchaseAsync(purchaseId, qrToken);

        if (tableId.HasValue && tableProc is not null && target is PurchaseStatus.Paid or PurchaseStatus.CheckedIn)
            await tableProc.MarkTableBookedAsync(tableId.Value);

        var tickets = await context.PurchaseTickets
            .Where(t => t.PurchaseId == purchaseId)
            .ToListAsync();
        if (tickets.Count == 0)
            return;

        var purchase = await context.Purchases.FirstAsync(b => b.Id == purchaseId);

        switch (target)
        {
            case PurchaseStatus.CheckedIn:
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
                purchase.Status = PurchaseStatus.CheckedIn;
                purchase.UpdatedAt = DateTime.UtcNow;
                break;

            case PurchaseStatus.Paid:
                // Natural mix so the demo UI exercises every ticket-action branch.
                foreach (var ticket in tickets)
                {
                    var roll = rng.NextDouble();
                    if (roll < 0.40)
                        AssignClaimed(ticket, users[rng.Next(users.Count)], rng);
                    else if (roll < 0.70)
                        AssignInvited(ticket);
                    // else keep Unassigned as produced by sp_confirm_purchase.
                }
                break;

            case PurchaseStatus.Cancelled:
                purchase.Status = PurchaseStatus.Cancelled;
                purchase.UpdatedAt = DateTime.UtcNow;
                break;

            case PurchaseStatus.Refunded:
                purchase.Status = PurchaseStatus.Refunded;
                purchase.UpdatedAt = DateTime.UtcNow;
                break;
        }

        await context.SaveChangesAsync();
    }

    private static void AssignClaimed(PurchaseTicket ticket, User claimer, Random rng)
    {
        ticket.GuestUserId = claimer.Id;
        ticket.ClaimedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 72));
        ticket.Status = TicketStatus.Claimed;
        ticket.InviteTokenHash = null;
        ticket.UpdatedAt = DateTime.UtcNow;
    }

    private static void AssignInvited(PurchaseTicket ticket)
    {
        // Token hash is a throwaway SHA-256 of a random blob — the seed flow never hands the
        // raw token anywhere, so nobody can actually claim these; they just render as "Invited"
        // in the UI until someone uses the live invite flow on a real purchase.
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        ticket.InviteTokenHash = hash;
        ticket.InviteExpiresAt = DateTime.UtcNow.AddDays(7);
        ticket.InvitedEmail = $"guest{Random.Shared.Next(1000, 9999)}@example.com";
        ticket.InviteSentAt = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(5, 120));
        ticket.Status = TicketStatus.Invited;
        ticket.UpdatedAt = DateTime.UtcNow;
    }

    private static PurchaseStatus PickStatus(Random rng, bool isFeatured = false)
    {
        var roll = rng.NextDouble();
        if (isFeatured)
        {
            return roll switch
            {
                < 0.60 => PurchaseStatus.Paid,
                < 0.75 => PurchaseStatus.CheckedIn,
                < 0.90 => PurchaseStatus.Pending,
                < 0.95 => PurchaseStatus.Cancelled,
                _ => PurchaseStatus.Refunded
            };
        }

        return roll switch
        {
            < 0.60 => PurchaseStatus.Paid,
            < 0.75 => PurchaseStatus.CheckedIn,
            < 0.90 => PurchaseStatus.Pending,
            < 0.95 => PurchaseStatus.Cancelled,
            _ => PurchaseStatus.Refunded
        };
    }

    private static async Task AddStripeTransactionAsync(
        IStripeTransactionProcedures stripeTransactionProc, Guid purchaseId,
        int amountCents, int transferAmountCents, PurchaseStatus status)
    {
        var paymentStatus = status switch
        {
            PurchaseStatus.Paid or PurchaseStatus.CheckedIn => "Succeeded",
            PurchaseStatus.Refunded => "Refunded",
            PurchaseStatus.Cancelled => "Failed",
            _ => "RequiresConfirmation"
        };

        var intentId = $"pi_seed_{Guid.NewGuid():N}";
        await stripeTransactionProc.CreateAsync(purchaseId, intentId, amountCents, transferAmountCents);

        if (paymentStatus != "RequiresConfirmation")
            await stripeTransactionProc.UpdateStatusAsync(intentId, paymentStatus);
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
