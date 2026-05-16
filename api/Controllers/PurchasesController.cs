using System.Security.Claims;
using Api.Exceptions;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Purchases;
using Contracts.DTOs.Tables;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/purchases")]
public class PurchasesController(
    IPurchaseService purchaseService,
    IPricingService pricingService,
    EventPlatformDbContext context,
    ISecretsProvider secrets
) : ControllerBase
{
    [HttpPost]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var purchase = await purchaseService.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = purchase.PurchaseId }, purchase);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] Create failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }

        catch (OrganizationNotPayoutReadyException ex) { Log.Warning("[Purchases] Create blocked by Connect enforcement: {Message}", ex.Message); return Conflict(new ApiError(409, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] Create failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQuote([FromBody] PricingQuoteRequest request)
    {
        try
        {
            var quote = await pricingService.CalculatePublicQuoteAsync(request);
            return Ok(quote);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] Quote failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] Quote failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("checkout-quote")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetCheckoutQuote([FromBody] PricingQuoteRequest request)
    {
        try
        {
            var quote = await pricingService.CalculateCheckoutQuoteAsync(request);
            return Ok(quote);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] CheckoutQuote failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] CheckoutQuote failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("{id:guid}/confirm")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ConfirmPayment(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var purchase = await purchaseService.ConfirmPaymentAsync(id, userId);
            return Ok(purchase);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] ConfirmPayment failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] ConfirmPayment failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Purchases] ConfirmPayment forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("confirm-by-intent")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ConfirmByPaymentIntent([FromBody] ConfirmByIntentRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var purchase = await context.PurchaseViews.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PaymentIntentId == request.PaymentIntentId);

            if (purchase is null)
                return NotFound(new ApiError(404, "Payment not found", HttpContext.TraceIdentifier));

            if (purchase.UserId != userId)
                return StatusCode(403, new ApiError(403, "Not your purchase", HttpContext.TraceIdentifier));

            if (purchase.Status != "Pending")
                return Ok(await purchaseService.GetByIdAsync(purchase.PurchaseId));

            var result = await purchaseService.ConfirmPaymentAsync(purchase.PurchaseId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] ConfirmByIntent failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] ConfirmByIntent failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Purchases] ConfirmByIntent forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var purchase = await purchaseService.CancelAsync(id, userId);
            return Ok(purchase);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] Cancel failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] Cancel failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Purchases] Cancel forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("cancel-beacon")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> CancelBeacon([FromBody] CancelBeaconRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var purchase = await context.PurchaseViews.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PurchaseId == request.PurchaseId);
            if (purchase is null) return Ok();
            if (purchase.UserId != userId)
            {
                Log.Warning("[Purchases] AUDIT beacon_cancel_ownership_mismatch purchase={PurchaseId} user={UserId}", request.PurchaseId, userId);
                return Ok();
            }

            await purchaseService.CancelAsync(request.PurchaseId, userId);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Purchases] Beacon cancel failed for purchase {PurchaseId}", request.PurchaseId);
            return Ok();
        }
    }

    [HttpPost("{id:guid}/refund")]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> Refund(Guid id)
    {
        var purchase = await context.PurchaseViews.AsNoTracking().FirstOrDefaultAsync(b => b.PurchaseId == id);
        if (purchase is null) return NotFound(new ApiError(404, "Purchase not found", HttpContext.TraceIdentifier));

        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == purchase.EventId);
        if (ev is not null && ev.BusinessUserId != adminId && !User.IsInRole(UserRole.Developer.ToString()))
            return StatusCode(403, new ApiError(403, "Not your event", HttpContext.TraceIdentifier));

        try
        {
            var result = await purchaseService.RefundAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] Refund failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] Refund failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
    }

    [HttpGet("{id:guid}")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var userRole = Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value);
        var purchase = await purchaseService.GetByIdAsync(id);
        if (purchase is null) return NotFound(new ApiError(404, "Purchase not found", HttpContext.TraceIdentifier));

        if (userRole < UserRole.Staff && purchase.UserId != userId)
            return StatusCode(403, new ApiError(403, "Not your purchase", HttpContext.TraceIdentifier));

        return Ok(purchase);
    }

    [HttpGet("mine")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyPurchases(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var query = context.PurchaseViews.AsNoTracking()
            .Where(b => b.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b =>
                b.EventTitle.ToLower().Contains(term) ||
                b.PurchaseNumber.ToLower().Contains(term) ||
                b.Status.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(b => new PurchaseDto(
            b.PurchaseId, b.PurchaseNumber, b.Status,
            b.UserId, $"{b.UserFirstName} {b.UserLastName}", b.EventId, b.EventTitle,
            b.EventStartDate, b.EventEndDate, b.EventCategory, b.EventImagePath,
            b.VenueName, !string.IsNullOrEmpty(b.VenueAddress) ? $"{b.VenueAddress}, {b.VenueCity}, {b.VenueState}" : null,
            null, b.TotalCents, null,
            b.TableId, b.TableLabel, b.TableLabels, b.SeatsReserved, b.EventTicketTypeId, b.EventTicketTypeLabel, b.TicketCount,
            b.StripeTransactionId.HasValue ? new StripeTransactionDto(
                b.StripeTransactionId.Value, b.PaymentIntentId!, b.PaymentStatus!,
                b.PaymentAmountCents ?? 0, b.TotalChargedCents, b.TaxAmountCents,
                b.StripeFeesCents, null, b.PaidAt, b.RefundedAt
            ) : null,
            b.CreatedAt
        )).ToList();

        return Ok(new PagedResponse<PurchaseDto>(dtos, total, page, pageSize));
    }

    [HttpGet("stripe-config")]
    [AllowAnonymous]
    public IActionResult GetStripeConfig([FromServices] IHostEnvironment env)
    {
        var publishableKey = secrets.StripePublishableKey;
        if (string.IsNullOrEmpty(publishableKey))
            return StatusCode(503, new ApiError(503, "Payment not configured", HttpContext.TraceIdentifier));

        var keyIsLive = publishableKey.StartsWith("pk_live_");
        if (env.IsProduction() && !keyIsLive)
            return StatusCode(503, new ApiError(503, "Payment not configured", HttpContext.TraceIdentifier));
        if (!env.IsProduction() && keyIsLive)
            return StatusCode(503, new ApiError(503, "Live Stripe keys are not permitted outside production", HttpContext.TraceIdentifier));

        var mode = env.IsProduction() && keyIsLive ? "live" : "test";
        return Ok(new StripeConfigDto(publishableKey, mode));
    }

    [HttpGet("{id:guid}/qr")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetQrCode(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var png = await purchaseService.GetQrImageAsync(id, userId);
            return File(png, "image/png");
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Purchases] GetQrCode failed: {Message}", ex.Message); return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Purchases] GetQrCode failed: {Message}", ex.Message); return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Purchases] GetQrCode forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, "Access denied", HttpContext.TraceIdentifier)); }
    }
}
