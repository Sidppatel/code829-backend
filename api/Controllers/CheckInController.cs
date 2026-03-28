using Api.Middleware;
using Contracts.DTOs.CheckIn;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[ApiController]
[Route("checkin")]
[Authorize]
[RequireRole(UserRole.Staff)]
public class CheckInController(EventPlatformDbContext context) : ControllerBase
{
    /// <summary>
    /// Scan a QR token to check in a booking.
    /// Validates the booking is Paid, prevents double check-in.
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new ScanResponse(false, "QR token is required", null, null, null, null, null, null));

        var booking = await context.Bookings
            .Include(b => b.User)
            .Include(b => b.Event)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.QrToken == request.QrToken);

        if (booking is null)
        {
            Log.Warning("[CheckIn] Invalid QR token scanned: {Token}", request.QrToken[..Math.Min(10, request.QrToken.Length)]);
            return NotFound(new ScanResponse(false, "Invalid QR code — booking not found", null, null, null, null, null, null));
        }

        if (booking.Status == BookingStatus.CheckedIn)
        {
            Log.Warning("[CheckIn] Double scan attempt for {BookingNumber}", booking.BookingNumber);
            return Conflict(new ScanResponse(
                false, "Already checked in",
                booking.BookingNumber, booking.User.Name, booking.Event.Title,
                booking.Status.ToString(), booking.Items.Count, booking.UpdatedAt
            ));
        }

        if (booking.Status != BookingStatus.Paid)
        {
            return BadRequest(new ScanResponse(
                false, $"Booking is {booking.Status} — cannot check in",
                booking.BookingNumber, booking.User.Name, booking.Event.Title,
                booking.Status.ToString(), booking.Items.Count, null
            ));
        }

        booking.Status = BookingStatus.CheckedIn;
        booking.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        Log.Information("[CheckIn] {BookingNumber} checked in for {Event}", booking.BookingNumber, booking.Event.Title);

        return Ok(new ScanResponse(
            true, "Check-in successful",
            booking.BookingNumber, booking.User.Name, booking.Event.Title,
            booking.Status.ToString(), booking.Items.Count, DateTime.UtcNow
        ));
    }

    /// <summary>
    /// Get check-in stats for an event.
    /// </summary>
    [HttpGet("events/{eventId:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var bookings = await context.Bookings
            .Where(b => b.EventId == eventId)
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var checkedIn = bookings.FirstOrDefault(b => b.Status == BookingStatus.CheckedIn)?.Count ?? 0;
        var paid = bookings.FirstOrDefault(b => b.Status == BookingStatus.Paid)?.Count ?? 0;
        var pending = bookings.FirstOrDefault(b => b.Status == BookingStatus.Pending)?.Count ?? 0;
        var total = bookings.Sum(b => b.Count);

        var totalTicketsSold = await context.BookingItems
            .Where(bi => bi.Booking.EventId == eventId &&
                   (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn))
            .CountAsync();

        var lastCheckIn = await context.Bookings
            .Where(b => b.EventId == eventId && b.Status == BookingStatus.CheckedIn)
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => (DateTime?)b.UpdatedAt)
            .FirstOrDefaultAsync();

        // Percentage based on paid+checked-in as denominator (bookings eligible for check-in)
        var eligible = paid + checkedIn;
        var percentage = eligible > 0 ? Math.Round(checkedIn * 100.0 / eligible, 1) : 0;

        return Ok(new CheckInStatsDto(
            eventId, ev.Title, total, checkedIn, pending, paid,
            percentage, totalTicketsSold, lastCheckIn
        ));
    }
}
