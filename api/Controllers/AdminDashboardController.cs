using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Admin;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminDashboardController(EventPlatformDbContext context) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalEvents = await context.Events.CountAsync();
        var publishedEvents = await context.Events.CountAsync(e => e.Status == EventStatus.Published);
        var totalBookings = await context.Bookings.CountAsync();
        var paidBookings = await context.Bookings.CountAsync(b => b.Status == BookingStatus.Paid);
        var checkedIn = await context.Bookings.CountAsync(b => b.Status == BookingStatus.CheckedIn);
        var revenueBookings = await context.Bookings
            .Where(b => b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn)
            .Select(b => b.TotalCents)
            .ToListAsync();
        var totalRevenue = revenueBookings.Sum(x => (long)x);
        var totalUsers = await context.Users.CountAsync();
        var totalVenues = await context.Venues.CountAsync();

        var topEventsRaw = await context.Bookings
            .Where(b => b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn)
            .GroupBy(b => new { b.EventId, b.Event.Title })
            .Select(g => new { g.Key.EventId, g.Key.Title, Count = g.Count(), Revenue = g.Sum(b => b.TotalCents) })
            .OrderByDescending(e => e.Revenue)
            .Take(10)
            .ToListAsync();
        var topEvents = topEventsRaw.Select(e => new EventRevenueDto(e.EventId, e.Title, e.Count, e.Revenue)).ToList();

        var bookingsByStatus = await context.Bookings
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var eventsByCategory = await context.Events
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);

        return Ok(new DashboardStatsDto(
            totalEvents, publishedEvents, totalBookings, paidBookings, checkedIn,
            totalRevenue, totalUsers, totalVenues, topEvents, bookingsByStatus, eventsByCategory
        ));
    }
}
