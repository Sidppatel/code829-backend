using Api.Middleware;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
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
    IDashboardProcedures dashboardProc) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var stats = await context.AdminDashboardStatsViews.AsNoTracking().FirstAsync(ct);
        var topRows = await context.TopEventRevenueViews.AsNoTracking().ToListAsync(ct);
        var statusRows = await context.PurchasesByStatusViews.AsNoTracking().ToListAsync(ct);
        var categoryRows = await context.EventsByCategoryViews.AsNoTracking().ToListAsync(ct);

        var topEvents = topRows
            .Select(r => new EventRevenueDto(r.EventId, r.Title, r.PurchaseCount, r.RevenueCents))
            .ToList();
        var purchasesByStatus = statusRows.ToDictionary(r => r.Status, r => r.Count);
        var eventsByCategory = categoryRows.ToDictionary(r => r.Category, r => r.Count);

        return Ok(new DashboardStatsDto(
            stats.TotalEvents, stats.PublishedEvents, stats.TotalPurchases,
            stats.PaidPurchases, stats.CheckedInPurchases,
            stats.TotalRevenueCents, stats.TotalUsers, stats.TotalVenues,
            topEvents, purchasesByStatus, eventsByCategory
        ));
    }

    [HttpGet("reports/monthly")]
    public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new ApiError(400, "Month must be between 1 and 12", HttpContext.TraceIdentifier));

        var summary = await dashboardProc.GetMonthlyReportSummaryAsync(year, month, ct);
        var rows = await dashboardProc.GetMonthlyReportByEventAsync(year, month, ct);
        var byEvent = rows
            .Select(r => new EventMonthlyBreakdown(
                r.EventId, r.EventTitle, r.PurchaseCount,
                r.ChargedCents, r.AdminPayoutCents, r.PlatformFeeCents,
                r.StripeFeesCents, r.TaxCollectedCents))
            .ToList();

        return Ok(new MonthlyReportDto(
            year, month, summary.TotalPurchases,
            summary.TotalChargedCents, summary.TotalAdminPayoutsCents,
            summary.TotalPlatformFeesCents, summary.TotalStripeFeesCents,
            summary.TotalTaxCollectedCents, summary.NetPlatformRevenueCents,
            byEvent));
    }

    [HttpGet("dashboard/next-event")]
    public async Task<IActionResult> GetNextEvent(CancellationToken ct)
    {
        var stats = await dashboardProc.GetNextEventDashboardAsync(DateTime.UtcNow, ct);
        if (stats is null)
            return Ok(new { hasUpcoming = false });

        var recent = await dashboardProc.GetEventRecentPurchasesAsync(stats.EventId, 8, ct);
        var recentPurchases = recent
            .Select(r => new RecentPurchaseDto(r.PurchaseId, r.PurchaseNumber, r.UserName, r.UserEmail, r.Status, r.TotalCents, r.CreatedAt))
            .ToList();

        return Ok(new
        {
            hasUpcoming = true,
            data = new NextEventDashboardDto(
                stats.EventId, stats.Title, stats.Slug, stats.Status, stats.Category,
                stats.StartDate, stats.EndDate, stats.VenueName, stats.VenueAddress, stats.VenueCity, stats.VenueState,
                stats.ImagePath, stats.LayoutMode, stats.DaysUntil,
                stats.TotalPurchases, stats.PaidPurchases, stats.CheckedInPurchases,
                stats.PendingPurchases, stats.CancelledPurchases, stats.RefundedPurchases,
                stats.RevenueCents, stats.PotentialRevenueCents, stats.TotalCapacity, stats.SoldCount,
                recentPurchases
            )
        });
    }
}
