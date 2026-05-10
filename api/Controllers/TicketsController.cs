using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Purchases;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}")]
public class TicketsController(
    EventPlatformDbContext context,
    ITicketProcedures ticketProc,
    IEmailService emailService,
    ISettingsService settings
) : ControllerBase
{

    [HttpGet("purchases/{purchaseId:guid}/tickets")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetTicketsForPurchase(Guid purchaseId)
    {
        var userId = GetUserId();

        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PurchaseId == purchaseId);
        if (purchase is null)
            return NotFound(new ApiError(404, "Purchase not found", HttpContext.TraceIdentifier));
        if (purchase.UserId != userId)
            return StatusCode(403, new ApiError(403, "Not your purchase", HttpContext.TraceIdentifier));

        var tickets = await context.PurchaseTicketViews.AsNoTracking()
            .Where(t => t.PurchaseId == purchaseId)
            .OrderBy(t => t.SeatNumber)
            .ToListAsync();

        var alreadyClaimedByMe = tickets.Any(t =>
            t.GuestUserId == userId &&
            (t.Status == nameof(TicketStatus.Claimed) || t.Status == nameof(TicketStatus.CheckedIn)));

        var dtos = tickets.Select(t => new PurchaseTicketDto(
            t.PurchaseTicketId, t.TicketCode, t.SeatNumber, t.Status,
            purchase.PurchaseId, purchase.PurchaseNumber,
            purchase.EventId, purchase.EventTitle, purchase.EventStartDate,
            purchase.VenueName,
            purchase.TableLabel,
            t.GuestFirstName is not null ? $"{t.GuestFirstName} {t.GuestLastName}" : null,
            t.GuestEmail,
            t.InvitedEmail, t.InviteSentAt, t.ClaimedAt,
            t.GuestUserId,
            !alreadyClaimedByMe &&
                (t.Status == nameof(TicketStatus.Unassigned) || t.Status == nameof(TicketStatus.Invited))
        ));

        return Ok(dtos);
    }

    [HttpGet("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/qr")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetTicketQr(Guid purchaseId, Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.PurchaseTicketViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PurchaseTicketId == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));

        if (ticket.PurchaseUserId != userId && ticket.GuestUserId != userId)
            return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier));

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return File(qrCode.GetGraphic(10), "image/png");
    }

    [HttpPost("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/invite")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> InviteGuest(Guid purchaseId, Guid ticketId, [FromBody] InviteTicketRequest request)
    {
        var userId = GetUserId();
        var ticket = await context.PurchaseTicketViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PurchaseTicketId == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.PurchaseUserId != userId)
            return StatusCode(403, new ApiError(403, "Only the purchase owner can invite guests", HttpContext.TraceIdentifier));
        if (ticket.Status == nameof(TicketStatus.CheckedIn))
            return BadRequest(new ApiError(400, "Cannot modify a checked-in ticket", HttpContext.TraceIdentifier));

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var invited = await ticketProc.SetInviteAsync(ticket.PurchaseTicketId, tokenHash, normalizedEmail, expiresAt);
        if (!invited)
            return Conflict(new ApiError(409, "This ticket has already been claimed. Revoke it first.", HttpContext.TraceIdentifier));

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var inviterName = $"{ticket.PurchaseUserFirstName} {ticket.PurchaseUserLastName}";
        var eventTitle = ticket.EventTitle;
        var eventDate = ticket.EventStartDate.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
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

    [HttpPost("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/claim-self")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ClaimSelf(Guid purchaseId, Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.PurchaseTicketViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PurchaseTicketId == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.PurchaseUserId != userId)
            return StatusCode(403, new ApiError(403, "Only the purchase owner can self-claim", HttpContext.TraceIdentifier));
        if (ticket.Status == nameof(TicketStatus.CheckedIn))
            return BadRequest(new ApiError(400, "Cannot modify a checked-in ticket", HttpContext.TraceIdentifier));
        if (ticket.Status == nameof(TicketStatus.Claimed) && ticket.GuestUserId == userId)
            return Ok(new { message = "Already claimed by you", ticketId = ticket.PurchaseTicketId });

        var result = await ticketProc.ClaimSelfAsync(ticket.PurchaseTicketId, userId);
        if (!result.Success)
            return Conflict(new ApiError(409, result.Message, HttpContext.TraceIdentifier));

        Log.Information("[Tickets] {TicketCode} self-claimed by owner {UserId}", ticket.TicketCode, userId);
        return Ok(new { message = result.Message, ticketId = ticket.PurchaseTicketId });
    }

    [HttpPost("purchases/{purchaseId:guid}/tickets/{ticketId:guid}/revoke")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> RevokeInvite(Guid purchaseId, Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.PurchaseTicketViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PurchaseTicketId == ticketId && t.PurchaseId == purchaseId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));
        if (ticket.PurchaseUserId != userId)
            return StatusCode(403, new ApiError(403, "Only the purchase owner can revoke invites", HttpContext.TraceIdentifier));
        if (ticket.Status == nameof(TicketStatus.CheckedIn))
            return BadRequest(new ApiError(400, "Cannot revoke a checked-in ticket", HttpContext.TraceIdentifier));

        await ticketProc.RevokeInviteAsync(ticket.PurchaseTicketId);

        Log.Information("[Tickets] Invite revoked for {TicketCode}", ticket.TicketCode);
        return Ok(new { message = "Invite revoked" });
    }

    [HttpGet("tickets/claim")]
    [AllowAnonymous]
    public async Task<IActionResult> GetClaimInfo([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new ApiError(400, "Token is required", HttpContext.TraceIdentifier));

        var tokenHash = HashToken(token);
        var ticket = await context.PurchaseTicketViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.InviteTokenHash == tokenHash);

        if (ticket is null)
            return NotFound(new ApiError(404, "Invalid or expired invite link", HttpContext.TraceIdentifier));

        if (ticket.InviteExpiresAt.HasValue && ticket.InviteExpiresAt < DateTime.UtcNow)
            return BadRequest(new ApiError(400, "This invite link has expired", HttpContext.TraceIdentifier));

        var tableLabel = ticket.PurchaseTableId.HasValue
            ? await context.TableViews.AsNoTracking()
                .Where(t => t.TableId == ticket.PurchaseTableId)
                .Select(t => t.Label)
                .FirstOrDefaultAsync()
            : null;

        return Ok(new TicketClaimInfoDto(
            ticket.PurchaseTicketId,
            ticket.TicketCode,
            ticket.SeatNumber,
            ticket.EventTitle,
            ticket.EventStartDate,
            ticket.VenueName,
            tableLabel,
            ticket.Status == nameof(TicketStatus.Claimed) || ticket.Status == nameof(TicketStatus.CheckedIn)
        ));
    }

    [HttpPost("tickets/claim")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ClaimTicket([FromBody] ClaimTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new ApiError(400, "Token is required", HttpContext.TraceIdentifier));

        var userId = GetUserId();
        var tokenHash = HashToken(request.Token);

        var result = await ticketProc.ClaimByTokenAsync(tokenHash, userId);

        if (!result.Success)
        {
            if (result.TicketId is null)
                return NotFound(new ApiError(404, result.Message, HttpContext.TraceIdentifier));
            return BadRequest(new ApiError(400, result.Message, HttpContext.TraceIdentifier));
        }

        if (result.AlreadyByMe)
        {
            Log.Information("[Tickets] {TicketId} re-claim attempt by same user {UserId}", result.TicketId, userId);
            return Ok(new { message = result.Message, ticketId = result.TicketId });
        }

        Log.Information("[Tickets] {TicketId} claimed by user {UserId}", result.TicketId, userId);
        return Ok(new { message = result.Message, ticketId = result.TicketId });
    }

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

        var purchaseIds = tickets.Select(t => t.PurchaseId).Distinct().ToList();
        var purchaseTableLabels = await context.PurchaseViews.AsNoTracking()
            .Where(b => purchaseIds.Contains(b.PurchaseId) && b.TableId.HasValue)
            .ToDictionaryAsync(b => b.PurchaseId, b => b.TableLabel);

        var dtos = tickets.Select(t => new GuestTicketDto(
            t.PurchaseTicketId,
            t.TicketCode,
            t.SeatNumber,
            t.Status,
            t.EventTitle,
            t.EventStartDate,
            t.VenueName,
            purchaseTableLabels.TryGetValue(t.PurchaseId, out var label) ? label : null,
            t.ClaimedAt
        ));

        return Ok(dtos);
    }

    [HttpGet("tickets/{ticketId:guid}/qr")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyTicketQr(Guid ticketId)
    {
        var userId = GetUserId();
        var ticket = await context.PurchaseTicketViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PurchaseTicketId == ticketId);

        if (ticket is null)
            return NotFound(new ApiError(404, "Ticket not found", HttpContext.TraceIdentifier));

        if (ticket.PurchaseUserId != userId && ticket.GuestUserId != userId)
            return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier));

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return File(qrCode.GetGraphic(10), "image/png");
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
