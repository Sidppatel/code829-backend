using Contracts.DTOs;
using Api.Middleware;
using Contracts.DTOs.CheckIn;
using Contracts.Enums;
using Db;
using Db.Entities.Views;
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
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new ScanResponse(false, "QR token is required", null, null, null, null, null));

        // ARCH-EXCEPTION: ticket scan requires ticket + booking + user + event in one query for
        // the response DTO. No dedicated view/SP wraps this join; the scan is read-heavy and
        // mutates via an existing SP (MarkTicketCheckedIn) later in the flow.
        var ticket = await context.BookingTickets
            .Include(t => t.Booking).ThenInclude(b => b.User)
            .Include(t => t.Booking).ThenInclude(b => b.Event)
            .Include(t => t.GuestUser)
            .FirstOrDefaultAsync(t => t.QrToken == request.QrToken);

        if (ticket is not null)
        {
            var guestName = ticket.GuestUser is not null
                ? $"{ticket.GuestUser.FirstName} {ticket.GuestUser.LastName}"
                : $"{ticket.Booking.User.FirstName} {ticket.Booking.User.LastName}";

            if (ticket.Status == Contracts.Enums.TicketStatus.CheckedIn)
            {
                Log.Warning("[CheckIn] Double scan for ticket {TicketCode}", ticket.TicketCode);
                return Conflict(new ScanResponse(
                    false, $"Ticket already checked in (Seat #{ticket.SeatNumber})",
                    ticket.Booking.BookingNumber, guestName, ticket.Booking.Event.Title,
                    "CheckedIn", ticket.UpdatedAt
                ));
            }

            if (ticket.Booking.Status != BookingStatus.Paid && ticket.Booking.Status != BookingStatus.CheckedIn)
            {
                return BadRequest(new ScanResponse(
                    false, $"Booking is {ticket.Booking.Status} — cannot check in",
                    ticket.Booking.BookingNumber, guestName, ticket.Booking.Event.Title,
                    ticket.Booking.Status.ToString(), null
                ));
            }

            // A ticket must be claimed by its actual attendee before it's usable. This prevents
            // a buyer (or anyone holding the QR) from walking in with an unclaimed seat — the
            // attendee's identity has to be on record first.
            if (ticket.Status != Contracts.Enums.TicketStatus.Claimed)
            {
                var reason = ticket.Status == Contracts.Enums.TicketStatus.Invited
                    ? "Ticket invite not yet accepted — recipient must claim it first"
                    : "Ticket has not been claimed yet — assign it to an attendee first";
                return BadRequest(new ScanResponse(
                    false, reason,
                    ticket.Booking.BookingNumber, guestName, ticket.Booking.Event.Title,
                    ticket.Status.ToString(), null
                ));
            }

            ticket.Status = Contracts.Enums.TicketStatus.CheckedIn;
            ticket.UpdatedAt = DateTime.UtcNow;

            // ARCH-EXCEPTION: scoped read (all tickets for this booking) to decide booking-level
            // CheckedIn rollup. Mutations happen on the already-tracked ticket entity above.
            var allTickets = await context.BookingTickets
                .Where(t => t.BookingId == ticket.BookingId)
                .ToListAsync();
            if (allTickets.All(t => t.Status == Contracts.Enums.TicketStatus.CheckedIn))
            {
                ticket.Booking.Status = BookingStatus.CheckedIn;
                ticket.Booking.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            Log.Information("[CheckIn] Ticket {TicketCode} (Seat #{Seat}) checked in for {Event}",
                ticket.TicketCode, ticket.SeatNumber, ticket.Booking.Event.Title);

            return Ok(new ScanResponse(
                true, $"Check-in successful — Seat #{ticket.SeatNumber}",
                ticket.Booking.BookingNumber, guestName, ticket.Booking.Event.Title,
                "CheckedIn", DateTime.UtcNow
            ));
        }

        // ARCH-EXCEPTION: legacy booking-level QR fallback. Needs booking + user + event joined
        // for the scan response; mutates the tracked booking status directly for check-in.
        var booking = await context.Bookings
            .Include(b => b.User)
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.QrToken == request.QrToken);

        if (booking is null)
        {
            Log.Warning("[CheckIn] Invalid QR token scanned: {Token}", request.QrToken[..Math.Min(10, request.QrToken.Length)]);
            return NotFound(new ScanResponse(false, "Invalid QR code — booking not found", null, null, null, null, null));
        }

        if (booking.Status == BookingStatus.CheckedIn)
        {
            Log.Warning("[CheckIn] Double scan attempt for {BookingNumber}", booking.BookingNumber);
            return Conflict(new ScanResponse(
                false, "Already checked in",
                booking.BookingNumber, $"{booking.User.FirstName} {booking.User.LastName}", booking.Event.Title,
                booking.Status.ToString(), booking.UpdatedAt
            ));
        }

        if (booking.Status != BookingStatus.Paid)
        {
            return BadRequest(new ScanResponse(
                false, $"Booking is {booking.Status} — cannot check in",
                booking.BookingNumber, $"{booking.User.FirstName} {booking.User.LastName}", booking.Event.Title,
                booking.Status.ToString(), null
            ));
        }

        booking.Status = BookingStatus.CheckedIn;
        booking.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        Log.Information("[CheckIn] {BookingNumber} checked in for {Event}", booking.BookingNumber, booking.Event.Title);

        return Ok(new ScanResponse(
            true, "Check-in successful",
            booking.BookingNumber, $"{booking.User.FirstName} {booking.User.LastName}", booking.Event.Title,
            booking.Status.ToString(), DateTime.UtcNow
        ));
    }

    [HttpGet("events/{eventId:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid eventId)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var bookings = await context.BookingViews.AsNoTracking()
            .Where(b => b.EventId == eventId)
            .ToListAsync();

        var paidOrCheckedIn = bookings.Where(b => b.Status is "Paid" or "CheckedIn").ToList();
        var checkedIn = paidOrCheckedIn.Where(b => b.Status == "CheckedIn").Sum(b => b.SeatsReserved ?? 1);
        var remaining = paidOrCheckedIn.Where(b => b.Status == "Paid").Sum(b => b.SeatsReserved ?? 1);
        var totalSold = checkedIn + remaining;
        var pending = bookings.Where(b => b.Status == "Pending").Sum(b => b.SeatsReserved ?? 1);

        // ARCH-EXCEPTION: LastCheckIn (booking.UpdatedAt) isn't projected on v_bookings. Single
        // scalar projection from the table for a stats display field; no business logic.
        var lastCheckIn = await context.Bookings
            .Where(b => b.EventId == eventId && b.Status == BookingStatus.CheckedIn)
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => (DateTime?)b.UpdatedAt)
            .FirstOrDefaultAsync();

        var percentage = totalSold > 0 ? Math.Round(checkedIn * 100.0 / totalSold, 1) : 0;

        return Ok(new CheckInStatsDto(
            eventId, ev.Title, totalSold, checkedIn, pending, remaining,
            percentage, lastCheckIn
        ));
    }
}
