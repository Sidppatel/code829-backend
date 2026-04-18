using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Purchases;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Entities.Views;
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
    //  Purchase owner: list all tickets for a purchase
    // ═══════════════════════════════════════════════════════════

    [HttpGet("purchases/{purchaseId:guid}/tickets")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetTicketsForPurchase(Guid purchaseId)
    {
        var userId = GetUserId();

        // Verify purchase ownership via view
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == purchaseId);
        if (purchase is null)
            return NotFound(new ApiError(404, "Purchase not found", HttpContext.TraceIdentifier));
        if (purchase.UserId != userId)
            return StatusCode(403, new ApiError(403, "Not your purchase", HttpContext.TraceIdentifier));

        var tickets = await context.PurchaseTicketViews.AsNoTracking()
            .Where(t => t.PurchaseId == purchaseId)
            .OrderBy(t => t.SeatNumber)
            .ToListAsync();

        var dtos = tickets.Select(t => new PurchaseTicketDto(
            t.Id, t.TicketCode, t.SeatNumber, t.Status,
            purchase.Id, purchase.PurchaseNumber,
            purchase.EventId, purchase.EventTitle, purchase.EventStartDate,
            purchase.VenueName,
            purchase.TableLabel,
            t.GuestFirstName is not null ? $"{t.GuestFirstName} {t.GuestLastName}" : null,
            t.GuestEmail,
            t.InvitedEmail, t.InviteSentAt, t.ClaimedAt,
            t.GuestUserId
        ));

        return Ok(dtos);
    }

    // ═══════════════════════════════════════════════════════════
    //  QR code image for a specific ticket
    // ═══════════════════════════════════════════════════════════

    [HttpGet("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/qr")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetTicketQr(Guid purchaseId, Guid ticketId)
    {
        var userId = GetUserId();
        // ARCH-EXCEPTION: ticket + owning purchase for ownership check on QR fetch.
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));

        // Allow purchase owner OR the assigned guest
        if (ticket.Purchase.UserId != userId && ticket.GuestUserId != userId)
            return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier));

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return File(qrCode.GetGraphic(10), "image/png");
    }

    // ═══════════════════════════════════════════════════════════
    //  Send invite email for a ticket
    // ═══════════════════════════════════════════════════════════

    [HttpPost("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/invite")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> InviteGuest(Guid purchaseId, Guid ticketId, [FromBody] InviteTicketRequest request)
    {
        var userId = GetUserId();
        // ARCH-EXCEPTION: ticket + purchase + owner user + event joined for invite email contents.
        // Mutation flows through the tracked entity (invite token/email set + SaveChanges).
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase).ThenInclude(b => b.User)
            .Include(t => t.Purchase).ThenInclude(b => b.Event)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.Purchase.UserId != userId)
            return StatusCode(403, new ApiError(403, "Only the purchase owner can invite guests", HttpContext.TraceIdentifier));
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
        var inviterName = $"{ticket.Purchase.User.FirstName} {ticket.Purchase.User.LastName}";
        var eventTitle = ticket.Purchase.Event.Title;
        var eventDate = ticket.Purchase.Event.StartDate.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
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
    //  Claim a ticket for the purchase owner (no email roundtrip)
    //  — buyer's "this one's for me" button.
    // ═══════════════════════════════════════════════════════════

    [HttpPost("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/claim-self")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ClaimSelf(Guid purchaseId, Guid ticketId)
    {
        var userId = GetUserId();
        // ARCH-EXCEPTION: ticket + purchase for ownership guard on self-claim.
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.Purchase.UserId != userId)
            return StatusCode(403, new ApiError(403, "Only the purchase owner can self-claim", HttpContext.TraceIdentifier));
        if (ticket.Status == TicketStatus.CheckedIn)
            return BadRequest(new ApiError(400, "Cannot modify a checked-in ticket", HttpContext.TraceIdentifier));
        if (ticket.Status == TicketStatus.Claimed && ticket.GuestUserId == userId)
            return Ok(new { message = "Already claimed by you", ticketId = ticket.Id });

        // Override any prior invite or guest-claim. Matches InviteGuest's "clear previous guest"
        // pattern so the buyer can always take a ticket back without a separate revoke step.
        ticket.GuestUserId = userId;
        ticket.Status = TicketStatus.Claimed;
        ticket.ClaimedAt = DateTime.UtcNow;
        ticket.InviteTokenHash = null;
        ticket.InviteExpiresAt = null;
        ticket.InvitedEmail = null;
        ticket.InviteSentAt = null;
        ticket.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        Log.Information("[Tickets] {TicketCode} self-claimed by owner {UserId}", ticket.TicketCode, userId);
        return Ok(new { message = "Ticket claimed", ticketId = ticket.Id });
    }

    // ═══════════════════════════════════════════════════════════
    //  Revoke a ticket invite
    // ═══════════════════════════════════════════════════════════

    [HttpPost("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/revoke")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> RevokeInvite(Guid purchaseId, Guid ticketId)
    {
        var userId = GetUserId();
        // ARCH-EXCEPTION: ticket + purchase for ownership guard on invite revoke.
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.Purchase.UserId != userId)
            return StatusCode(403, new ApiError(403, "Only the purchase owner can revoke invites", HttpContext.TraceIdentifier));
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
        // ARCH-EXCEPTION: claim-info needs ticket + purchase + inviter + event + venue for the
        // invite preview page. No composite view exists for this specific read shape.
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase).ThenInclude(b => b.User)
            .Include(t => t.Purchase).ThenInclude(b => b.Event).ThenInclude(e => e.Venue)
            .FirstOrDefaultAsync(t => t.InviteTokenHash == tokenHash);

        if (ticket is null)
            return NotFound(new ApiError(404, "Invalid or expired invite link", HttpContext.TraceIdentifier));

        if (ticket.InviteExpiresAt.HasValue && ticket.InviteExpiresAt < DateTime.UtcNow)
            return BadRequest(new ApiError(400, "This invite link has expired", HttpContext.TraceIdentifier));

        var tableLabel = ticket.Purchase.TableId.HasValue
            ? await context.TableViews.AsNoTracking().Where(t => t.Id == ticket.Purchase.TableId).Select(t => t.Label).FirstOrDefaultAsync()
            : null;

        return Ok(new TicketClaimInfoDto(
            ticket.Id,
            ticket.TicketCode,
            ticket.SeatNumber,
            ticket.Purchase.Event.Title,
            ticket.Purchase.Event.StartDate,
            ticket.Purchase.Event.Venue?.Name ?? "",
            tableLabel,
            $"{ticket.Purchase.User.FirstName} {ticket.Purchase.User.LastName}",
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

        // ARCH-EXCEPTION: claim-by-token needs ticket + purchase; mutation sets guest info
        // directly on the tracked ticket entity.
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase)
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

        var tickets = await context.PurchaseTicketViews.AsNoTracking()
            .Where(t => t.GuestUserId == userId)
            .OrderByDescending(t => t.EventStartDate)
            .ToListAsync();

        // Get table labels from purchase views for table purchases
        var purchaseIds = tickets.Select(t => t.PurchaseId).Distinct().ToList();
        var purchaseTableLabels = await context.PurchaseViews.AsNoTracking()
            .Where(b => purchaseIds.Contains(b.Id) && b.TableId.HasValue)
            .ToDictionaryAsync(b => b.Id, b => b.TableLabel);

        var dtos = tickets.Select(t => new GuestTicketDto(
            t.Id,
            t.TicketCode,
            t.SeatNumber,
            t.Status,
            t.EventTitle,
            t.EventStartDate,
            t.VenueName,
            purchaseTableLabels.TryGetValue(t.PurchaseId, out var label) ? label : null,
            t.PurchaseNumber,
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
        // ARCH-EXCEPTION: ticket + purchase for guest QR access (guest-vs-owner check).
        var ticket = await context.PurchaseTickets
            .Include(t => t.Purchase)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));

        // Allow purchase owner OR assigned guest
        if (ticket.Purchase.UserId != userId && ticket.GuestUserId != userId)
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
