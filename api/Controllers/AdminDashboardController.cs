using Api.Middleware;
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
            .Select(g => new { Category = g.Key != null ? g.Key.ToString()! : "Unknown", Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);

        return Ok(new DashboardStatsDto(
            totalEvents, publishedEvents, totalBookings, paidBookings, checkedIn,
            totalRevenue, totalUsers, totalVenues, topEvents, bookingsByStatus, eventsByCategory
        ));
    }

    [HttpGet("dashboard/next-event")]
    public virtual async Task<IActionResult> GetNextEvent()
    {
        var now = DateTime.UtcNow;
        var ev = await context.Events
            .Include(e => e.Venue).ThenInclude(v => v.Address)
            .Where(e => e.Status == EventStatus.Published && e.StartDate > now)
            .OrderBy(e => e.StartDate)
            .FirstOrDefaultAsync();

        if (ev is null)
        {
            ev = await context.Events
                .Include(e => e.Venue).ThenInclude(v => v.Address)
                .Where(e => e.StartDate > now && e.Status != EventStatus.Cancelled)
                .OrderBy(e => e.StartDate)
                .FirstOrDefaultAsync();
        }

        if (ev is null)
            return Ok(new { hasUpcoming = false });

        var bookings = await context.Bookings
            .Where(b => b.EventId == ev.Id)
            .Select(b => new { b.Id, b.BookingNumber, b.Status, b.TotalCents, b.CreatedAt,
                               UserName = b.User.FirstName + " " + b.User.LastName, UserEmail = b.User.Email })
            .ToListAsync();

        var paid = bookings.Count(b => b.Status == BookingStatus.Paid);
        var checkedInCount = bookings.Count(b => b.Status == BookingStatus.CheckedIn);
        var pending = bookings.Count(b => b.Status == BookingStatus.Pending);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var refunded = bookings.Count(b => b.Status == BookingStatus.Refunded);
        var revenue = bookings
            .Where(b => b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn)
            .Sum(b => (long)b.TotalCents);

        // Capacity from event tables or MaxCapacity
        var tables = await context.Tables
            .Include(t => t.EventTable)
            .Where(t => t.EventId == ev.Id && t.IsActive)
            .ToListAsync();
        var totalCapacity = ev.MaxCapacity ?? tables.Sum(t => t.EventTable.Capacity);
        var soldCount = paid + checkedInCount;

        // Potential revenue
        long potentialRevenue;
        if (ev.LayoutMode == LayoutMode.Open && ev.PricePerPersonCents.HasValue)
        {
            potentialRevenue = (long)ev.PricePerPersonCents.Value * totalCapacity;
        }
        else
        {
            potentialRevenue = tables.Sum(t => (long)t.EventTable.PriceCents);
        }

        var recentBookings = bookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .Select(b => new RecentBookingDto(b.Id, b.BookingNumber, b.UserName, b.UserEmail,
                b.Status.ToString(), b.TotalCents, b.CreatedAt))
            .ToList();

        var daysUntil = (int)Math.Ceiling((ev.StartDate - now).TotalDays);

        return Ok(new
        {
            hasUpcoming = true,
            data = new NextEventDashboardDto(
                ev.Id, ev.Title, ev.Slug, ev.Status.ToString(), (ev.Category?.ToString() ?? ""),
                ev.StartDate, ev.EndDate, ev.Venue.Name, ev.Venue.Address?.Line1 ?? "", ev.Venue.Address?.City ?? "", ev.Venue.Address?.State ?? "",
                ev.ImagePath, ev.LayoutMode.ToString(), daysUntil,
                bookings.Count, paid, checkedInCount, pending, cancelled, refunded,
                revenue, potentialRevenue, totalCapacity, soldCount,
                recentBookings
            )
        });
    }
}
