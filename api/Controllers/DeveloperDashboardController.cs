using Api.Middleware;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("developer")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperDashboardController(
    EventPlatformDbContext context,
    Db.Repositories.StoredProcedures.IUserProcedures userProc) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalEvents = await context.EventViews.AsNoTracking().CountAsync();
        var publishedEvents = await context.EventViews.AsNoTracking().CountAsync(e => e.Status == "Published");
        var totalBookings = await context.BookingViews.AsNoTracking().CountAsync();
        var paidBookings = await context.BookingViews.AsNoTracking().CountAsync(b => b.Status == "Paid");
        var checkedIn = await context.BookingViews.AsNoTracking().CountAsync(b => b.Status == "CheckedIn");
        var revenueList = await context.BookingViews.AsNoTracking()
            .Where(b => b.Status == "Paid" || b.Status == "CheckedIn")
            .Select(b => b.TotalCents)
            .ToListAsync();
        var totalRevenue = revenueList.Sum(x => (long)x);
        var totalUsers = (await userProc.GetCountsAsync()).Total;
        var totalVenues = await context.VenueViews.AsNoTracking().CountAsync();

        var topEventsRaw = await context.BookingViews.AsNoTracking()
            .Where(b => b.Status == "Paid" || b.Status == "CheckedIn")
            .GroupBy(b => new { b.EventId, b.EventTitle })
            .Select(g => new { g.Key.EventId, g.Key.EventTitle, Count = g.Count(), Revenue = g.Sum(b => b.TotalCents) })
            .OrderByDescending(e => e.Revenue)
            .Take(10)
            .ToListAsync();
        var topEvents = topEventsRaw.Select(e => new EventRevenueDto(e.EventId, e.EventTitle, e.Count, e.Revenue)).ToList();

        var bookingsByStatus = await context.BookingViews.AsNoTracking()
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var eventsByCategory = await context.EventViews.AsNoTracking()
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);

        return Ok(new DashboardStatsDto(
            totalEvents, publishedEvents, totalBookings, paidBookings, checkedIn,
            totalRevenue, totalUsers, totalVenues, topEvents, bookingsByStatus, eventsByCategory
        ));
    }

    [HttpGet("reports/monthly")]
    public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month)
    {
        if (month < 1 || month > 12)
            return BadRequest(new ApiError(400, "Month must be between 1 and 12", HttpContext.TraceIdentifier));

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var bookings = await context.BookingViews.AsNoTracking()
            .Where(b => (b.Status == "Paid" || b.Status == "CheckedIn")
                && b.PaidAt >= from && b.PaidAt < to)
            .ToListAsync();

        var totalBookings = bookings.Count;
        var totalChargedCents = bookings.Sum(b => (long)(b.TotalChargedCents ?? b.TotalCents));
        var totalAdminPayoutsCents = bookings.Sum(b => (long)(b.TransferAmountCents ?? b.SubtotalCents));
        var totalPlatformFeesCents = bookings.Sum(b => (long)b.FeeCents);
        var totalStripeFeesCents = bookings.Sum(b => (long)(b.StripeFeesCents ?? 0));
        var totalTaxCollectedCents = bookings.Sum(b => (long)(b.TaxAmountCents ?? 0));
        var netPlatformRevenueCents = totalPlatformFeesCents - totalStripeFeesCents;

        var byEvent = bookings
            .GroupBy(b => new { b.EventId, b.EventTitle })
            .Select(g => new EventMonthlyBreakdown(
                g.Key.EventId,
                g.Key.EventTitle,
                g.Count(),
                g.Sum(b => (long)(b.TotalChargedCents ?? b.TotalCents)),
                g.Sum(b => (long)(b.TransferAmountCents ?? b.SubtotalCents)),
                g.Sum(b => (long)b.FeeCents),
                g.Sum(b => (long)(b.StripeFeesCents ?? 0)),
                g.Sum(b => (long)(b.TaxAmountCents ?? 0))
            ))
            .OrderByDescending(e => e.ChargedCents)
            .ToList();

        return Ok(new MonthlyReportDto(
            year, month, totalBookings, totalChargedCents, totalAdminPayoutsCents,
            totalPlatformFeesCents, totalStripeFeesCents, totalTaxCollectedCents,
            netPlatformRevenueCents, byEvent));
    }

    [HttpGet("dashboard/next-event")]
    public async Task<IActionResult> GetNextEvent()
    {
        var now = DateTime.UtcNow;
        var ev = await context.EventViews.AsNoTracking()
            .Where(e => e.Status == "Published" && e.StartDate > now)
            .OrderBy(e => e.StartDate)
            .FirstOrDefaultAsync();

        if (ev is null)
        {
            ev = await context.EventViews.AsNoTracking()
                .Where(e => e.StartDate > now && e.Status != "Cancelled")
                .OrderBy(e => e.StartDate)
                .FirstOrDefaultAsync();
        }

        if (ev is null)
            return Ok(new { hasUpcoming = false });

        var bookings = await context.BookingViews.AsNoTracking()
            .Where(b => b.EventId == ev.Id)
            .ToListAsync();

        var paid = bookings.Count(b => b.Status == "Paid");
        var checkedInCount = bookings.Count(b => b.Status == "CheckedIn");
        var pending = bookings.Count(b => b.Status == "Pending");
        var cancelled = bookings.Count(b => b.Status == "Cancelled");
        var refunded = bookings.Count(b => b.Status == "Refunded");
        var revenue = bookings
            .Where(b => b.Status is "Paid" or "CheckedIn")
            .Sum(b => (long)b.TotalCents);

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == ev.Id && t.IsActive)
            .ToListAsync();
        var totalCapacity = ev.MaxCapacity ?? tables.Sum(t => t.Capacity);
        var soldCount = paid + checkedInCount;

        long potentialRevenue;
        if (ev.LayoutMode == "Open" && ev.PricePerPersonCents.HasValue)
            potentialRevenue = (long)ev.PricePerPersonCents.Value * totalCapacity;
        else
            potentialRevenue = tables.Sum(t => (long)t.PriceCents);

        var recentBookings = bookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .Select(b => new RecentBookingDto(b.Id, b.BookingNumber,
                $"{b.UserFirstName} {b.UserLastName}", b.UserEmail,
                b.Status, b.TotalCents, b.CreatedAt))
            .ToList();

        var daysUntil = (int)Math.Ceiling((ev.StartDate - now).TotalDays);

        return Ok(new
        {
            hasUpcoming = true,
            data = new NextEventDashboardDto(
                ev.Id, ev.Title, ev.Slug, ev.Status, ev.Category,
                ev.StartDate, ev.EndDate, ev.VenueName, ev.VenueAddress, ev.VenueCity, ev.VenueState,
                ev.ImagePath, ev.LayoutMode, daysUntil,
                bookings.Count, paid, checkedInCount, pending, cancelled, refunded,
                revenue, potentialRevenue, totalCapacity, soldCount,
                recentBookings
            )
        });
    }
}
