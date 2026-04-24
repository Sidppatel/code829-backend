using Api.Middleware;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/developer")]
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
        var totalPurchases = await context.PurchaseViews.AsNoTracking().CountAsync();
        var paidPurchases = await context.PurchaseViews.AsNoTracking().CountAsync(b => b.Status == "Paid");
        var checkedIn = await context.PurchaseViews.AsNoTracking().CountAsync(b => b.Status == "CheckedIn");
        var revenueList = await context.PurchaseViews.AsNoTracking()
            .Where(b => b.Status == "Paid" || b.Status == "CheckedIn")
            .Select(b => b.TotalCents)
            .ToListAsync();
        var totalRevenue = revenueList.Sum(x => (long)x);
        var totalUsers = (await userProc.GetCountsAsync()).Total;
        var totalVenues = await context.VenueViews.AsNoTracking().CountAsync();

        var topEventsRaw = await context.PurchaseViews.AsNoTracking()
            .Where(b => b.Status == "Paid" || b.Status == "CheckedIn")
            .GroupBy(b => new { b.EventId, b.EventTitle })
            .Select(g => new { g.Key.EventId, g.Key.EventTitle, Count = g.Count(), Revenue = g.Sum(b => b.TotalCents) })
            .OrderByDescending(e => e.Revenue)
            .Take(10)
            .ToListAsync();
        var topEvents = topEventsRaw.Select(e => new EventRevenueDto(e.EventId, e.EventTitle, e.Count, e.Revenue)).ToList();

        var purchasesByStatus = await context.PurchaseViews.AsNoTracking()
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var eventsByCategory = await context.EventViews.AsNoTracking()
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);

        return Ok(new DashboardStatsDto(
            totalEvents, publishedEvents, totalPurchases, paidPurchases, checkedIn,
            totalRevenue, totalUsers, totalVenues, topEvents, purchasesByStatus, eventsByCategory
        ));
    }

    [HttpGet("reports/monthly")]
    public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month)
    {
        if (month < 1 || month > 12)
            return BadRequest(new ApiError(400, "Month must be between 1 and 12", HttpContext.TraceIdentifier));

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var purchases = await context.PurchaseViews.AsNoTracking()
            .Where(b => (b.Status == "Paid" || b.Status == "CheckedIn")
                && b.PaidAt >= from && b.PaidAt < to)
            .ToListAsync();

        var totalPurchases = purchases.Count;
        var totalChargedCents = purchases.Sum(b => (long)(b.TotalChargedCents ?? b.TotalCents));
        var totalAdminPayoutsCents = purchases.Sum(b => (long)(b.TransferAmountCents ?? b.SubtotalCents));
        var totalPlatformFeesCents = purchases.Sum(b => (long)b.FeeCents);
        var totalStripeFeesCents = purchases.Sum(b => (long)(b.StripeFeesCents ?? 0));
        var totalTaxCollectedCents = purchases.Sum(b => (long)(b.TaxAmountCents ?? 0));
        var netPlatformRevenueCents = totalPlatformFeesCents - totalStripeFeesCents;

        var byEvent = purchases
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
            year, month, totalPurchases, totalChargedCents, totalAdminPayoutsCents,
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

        var purchases = await context.PurchaseViews.AsNoTracking()
            .Where(b => b.EventId == ev.EventId)
            .ToListAsync();

        var paid = purchases.Count(b => b.Status == "Paid");
        var checkedInCount = purchases.Count(b => b.Status == "CheckedIn");
        var pending = purchases.Count(b => b.Status == "Pending");
        var cancelled = purchases.Count(b => b.Status == "Cancelled");
        var refunded = purchases.Count(b => b.Status == "Refunded");
        var revenue = purchases
            .Where(b => b.Status is "Paid" or "CheckedIn")
            .Sum(b => (long)b.TotalCents);

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == ev.EventId && t.IsActive)
            .ToListAsync();
        var totalCapacity = ev.MaxCapacity ?? tables.Sum(t => t.Capacity);
        var soldCount = paid + checkedInCount;

        long potentialRevenue;
        if (ev.LayoutMode == "Open" && ev.PricePerPersonCents.HasValue)
            potentialRevenue = (long)ev.PricePerPersonCents.Value * totalCapacity;
        else
            potentialRevenue = tables.Sum(t => (long)t.PriceCents);

        var recentPurchases = purchases
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .Select(b => new RecentPurchaseDto(b.PurchaseId, b.PurchaseNumber,
                $"{b.UserFirstName} {b.UserLastName}", b.UserEmail,
                b.Status, b.TotalCents, b.CreatedAt))
            .ToList();

        var daysUntil = (int)Math.Ceiling((ev.StartDate - now).TotalDays);

        return Ok(new
        {
            hasUpcoming = true,
            data = new NextEventDashboardDto(
                ev.EventId, ev.Title, ev.Slug, ev.Status, ev.Category,
                ev.StartDate, ev.EndDate, ev.VenueName, ev.VenueAddress, ev.VenueCity, ev.VenueState,
                ev.ImagePath, ev.LayoutMode, daysUntil,
                purchases.Count, paid, checkedInCount, pending, cancelled, refunded,
                revenue, potentialRevenue, totalCapacity, soldCount,
                recentPurchases
            )
        });
    }
}
