using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;

namespace Api.Controllers;

[ApiController]
[Route("")]
public class TicketsController(
    EventPlatformDbContext context,
    IEmailService emailService,
    ISettingsService settings
) : ControllerBase
{
    // ═══════════════════════════════════════════════════════════
    //  Booking owner: list all tickets for a booking
    // ═══════════════════════════════════════════════════════════

    [HttpGet("bookings/{bookingId:guid}/tickets")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetTicketsForBooking(Guid bookingId)
    {
        var userId = GetUserId();
        var booking = await context.Bookings
            .Include(b => b.Event).ThenInclude(e => e.Venue)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
            return NotFound(new ApiError(404, "Booking not found", HttpContext.TraceIdentifier));
        if (booking.UserId != userId)
            return StatusCode(403, new ApiError(403, "Not your booking", HttpContext.TraceIdentifier));

        var tickets = await context.BookingTickets
            .Include(t => t.GuestUser)
            .Where(t => t.BookingId == bookingId)
            .OrderBy(t => t.SeatNumber)
            .ToListAsync();

        var tableLabel = booking.TableId.HasValue
            ? await context.Tables.Where(t => t.Id == booking.TableId).Select(t => t.Label).FirstOrDefaultAsync()
            : null;

        var dtos = tickets.Select(t => new BookingTicketDto(
            t.Id, t.TicketCode, t.SeatNumber, t.Status.ToString(),
            booking.Id, booking.BookingNumber,
            booking.EventId, booking.Event.Title, booking.Event.StartDate,
            booking.Event.Venue?.Name ?? "",
            tableLabel,
            t.GuestUser is not null ? $"{t.GuestUser.FirstName} {t.GuestUser.LastName}" : null,
            t.GuestUser?.Email,
            t.InvitedEmail, t.InviteSentAt, t.ClaimedAt
        ));

        return Ok(dtos);
    }

    // ═══════════════════════════════════════════════════════════
    //  QR code image for a specific ticket
    // ═══════════════════════════════════════════════════════════

    [HttpGet("bookings/{bookingId:guid}/tickets/{ticketId:guid}/qr")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetTicketQr(Guid bookingId, Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.BookingTickets
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.BookingId == bookingId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));

        // Allow booking owner OR the assigned guest
        if (ticket.Booking.UserId != userId && ticket.GuestUserId != userId)
            return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier));

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return File(qrCode.GetGraphic(10), "image/png");
    }

    // ═══════════════════════════════════════════════════════════
    //  Send invite email for a ticket
    // ═══════════════════════════════════════════════════════════

    [HttpPost("bookings/{bookingId:guid}/tickets/{ticketId:guid}/invite")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> InviteGuest(Guid bookingId, Guid ticketId, [FromBody] InviteTicketRequest request)
    {
        var userId = GetUserId();
        var ticket = await context.BookingTickets
            .Include(t => t.Booking).ThenInclude(b => b.User)
            .Include(t => t.Booking).ThenInclude(b => b.Event)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.BookingId == bookingId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.Booking.UserId != userId)
            return StatusCode(403, new ApiError(403, "Only the booking owner can invite guests", HttpContext.TraceIdentifier));
        if (ticket.Status == TicketStatus.CheckedIn)
            return BadRequest(new ApiError(400, "Cannot modify a checked-in ticket", HttpContext.TraceIdentifier));

        // Generate invite token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);

        ticket.InviteTokenHash = tokenHash;
        ticket.InviteExpiresAt = DateTime.UtcNow.AddDays(7);
        ticket.InvitedEmail = request.Email.ToLowerInvariant().Trim();
        ticket.InviteSentAt = DateTime.UtcNow;
        ticket.Status = TicketStatus.Invited;
        // Clear previous guest if re-inviting
        ticket.GuestUserId = null;
        ticket.ClaimedAt = null;
        ticket.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Send invite email
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var inviterName = $"{ticket.Booking.User.FirstName} {ticket.Booking.User.LastName}";
        var eventTitle = ticket.Booking.Event.Title;
        var eventDate = ticket.Booking.Event.StartDate.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
        var claimUrl = $"{frontendUrl}/tickets/claim?token={Uri.EscapeDataString(rawToken)}";

        await emailService.SendAsync(
            request.Email,
            $"You're invited! {eventTitle} | {appName}",
            EmailTemplates.TicketInvite(
                appName, request.GuestName ?? "", inviterName,
                eventTitle, eventDate, ticket.SeatNumber, claimUrl)
        );

        Log.Information("[Tickets] Invite sent for {TicketCode} to {Email}", ticket.TicketCode, request.Email);
        return Ok(new { message = $"Invite sent to {request.Email}" });
    }

    // ═══════════════════════════════════════════════════════════
    //  Revoke a ticket invite
    // ═══════════════════════════════════════════════════════════

    [HttpPost("bookings/{bookingId:guid}/tickets/{ticketId:guid}/revoke")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> RevokeInvite(Guid bookingId, Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.BookingTickets
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.BookingId == bookingId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.Booking.UserId != userId)
            return StatusCode(403, new ApiError(403, "Only the booking owner can revoke invites", HttpContext.TraceIdentifier));
        if (ticket.Status == TicketStatus.CheckedIn)
            return BadRequest(new ApiError(400, "Cannot revoke a checked-in ticket", HttpContext.TraceIdentifier));

        ticket.Status = TicketStatus.Unassigned;
        ticket.InviteTokenHash = null;
        ticket.InviteExpiresAt = null;
        ticket.InvitedEmail = null;
        ticket.InviteSentAt = null;
        ticket.GuestUserId = null;
        ticket.ClaimedAt = null;
        ticket.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        Log.Information("[Tickets] Invite revoked for {TicketCode}", ticket.TicketCode);
        return Ok(new { message = "Invite revoked" });
    }

    // ═══════════════════════════════════════════════════════════
    //  Validate invite token (anonymous — before login)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("tickets/claim")]
    [AllowAnonymous]
    public async Task<IActionResult> GetClaimInfo([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new ApiError(400, "Token is required", HttpContext.TraceIdentifier));

        var tokenHash = HashToken(token);
        var ticket = await context.BookingTickets
            .Include(t => t.Booking).ThenInclude(b => b.User)
            .Include(t => t.Booking).ThenInclude(b => b.Event).ThenInclude(e => e.Venue)
            .FirstOrDefaultAsync(t => t.InviteTokenHash == tokenHash);

        if (ticket is null)
            return NotFound(new ApiError(404, "Invalid or expired invite link", HttpContext.TraceIdentifier));

        if (ticket.InviteExpiresAt.HasValue && ticket.InviteExpiresAt < DateTime.UtcNow)
            return BadRequest(new ApiError(400, "This invite link has expired", HttpContext.TraceIdentifier));

        var tableLabel = ticket.Booking.TableId.HasValue
            ? await context.Tables.Where(t => t.Id == ticket.Booking.TableId).Select(t => t.Label).FirstOrDefaultAsync()
            : null;

        return Ok(new TicketClaimInfoDto(
            ticket.Id,
            ticket.TicketCode,
            ticket.SeatNumber,
            ticket.Booking.Event.Title,
            ticket.Booking.Event.StartDate,
            ticket.Booking.Event.Venue?.Name ?? "",
            tableLabel,
            $"{ticket.Booking.User.FirstName} {ticket.Booking.User.LastName}",
            ticket.Status == TicketStatus.Claimed || ticket.Status == TicketStatus.CheckedIn
        ));
    }

    // ═══════════════════════════════════════════════════════════
    //  Claim a ticket (authenticated)
    // ═══════════════════════════════════════════════════════════

    [HttpPost("tickets/claim")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ClaimTicket([FromBody] ClaimTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new ApiError(400, "Token is required", HttpContext.TraceIdentifier));

        var userId = GetUserId();
        var tokenHash = HashToken(request.Token);

        var ticket = await context.BookingTickets
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.InviteTokenHash == tokenHash);

        if (ticket is null)
            return NotFound(new ApiError(404, "Invalid or expired invite link", HttpContext.TraceIdentifier));

        if (ticket.InviteExpiresAt.HasValue && ticket.InviteExpiresAt < DateTime.UtcNow)
            return BadRequest(new ApiError(400, "This invite link has expired", HttpContext.TraceIdentifier));

        if (ticket.Status == TicketStatus.CheckedIn)
            return BadRequest(new ApiError(400, "This ticket has already been used", HttpContext.TraceIdentifier));

        if (ticket.GuestUserId == userId)
            return Ok(new { message = "You've already claimed this ticket", ticketId = ticket.Id });

        if (ticket.Status == TicketStatus.Claimed)
            return BadRequest(new ApiError(400, "This ticket has already been claimed", HttpContext.TraceIdentifier));

        ticket.GuestUserId = userId;
        ticket.Status = TicketStatus.Claimed;
        ticket.ClaimedAt = DateTime.UtcNow;
        ticket.InviteTokenHash = null;
        ticket.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        Log.Information("[Tickets] {TicketCode} claimed by user {UserId}", ticket.TicketCode, userId);
        return Ok(new { message = "Ticket claimed successfully", ticketId = ticket.Id });
    }

    // ═══════════════════════════════════════════════════════════
    //  My tickets (all tickets assigned to current user)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("tickets/mine")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyTickets()
    {
        var userId = GetUserId();

        var tickets = await context.BookingTickets
            .Include(t => t.Booking).ThenInclude(b => b.Event).ThenInclude(e => e.Venue)
            .Where(t => t.GuestUserId == userId)
            .OrderByDescending(t => t.Booking.Event.StartDate)
            .ToListAsync();

        // Get table labels for table bookings
        var tableIds = tickets
            .Where(t => t.Booking.TableId.HasValue)
            .Select(t => t.Booking.TableId!.Value)
            .Distinct()
            .ToList();
        var tableLabels = tableIds.Count > 0
            ? await context.Tables.Where(t => tableIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Label)
            : new Dictionary<Guid, string>();

        var dtos = tickets.Select(t => new GuestTicketDto(
            t.Id,
            t.TicketCode,
            t.SeatNumber,
            t.Status.ToString(),
            t.Booking.Event.Title,
            t.Booking.Event.StartDate,
            t.Booking.Event.Venue?.Name ?? "",
            t.Booking.TableId.HasValue && tableLabels.TryGetValue(t.Booking.TableId.Value, out var label) ? label : null,
            t.Booking.BookingNumber,
            t.ClaimedAt
        ));

        return Ok(dtos);
    }

    // ═══════════════════════════════════════════════════════════
    //  Guest QR — get QR for own ticket by ticket ID only
    // ═══════════════════════════════════════════════════════════

    [HttpGet("tickets/{ticketId:guid}/qr")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyTicketQr(Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.BookingTickets
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));

        // Allow booking owner OR assigned guest
        if (ticket.Booking.UserId != userId && ticket.GuestUserId != userId)
            return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier));

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return File(qrCode.GetGraphic(10), "image/png");
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
