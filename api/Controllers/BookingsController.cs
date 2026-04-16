using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Bookings;
using Contracts.DTOs.Tables;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[ApiController]
[Route("bookings")]
public class BookingsController(
    IBookingService bookingService,
    EventPlatformDbContext context,
    ISecretsProvider secrets
) : ControllerBase
{
    [HttpPost]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var booking = await bookingService.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Bookings] Create failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Bookings] Create failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
    }

    [HttpPost("{id:guid}/confirm")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ConfirmPayment(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var booking = await bookingService.ConfirmPaymentAsync(id, userId);
            return Ok(booking);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Bookings] ConfirmPayment failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Bookings] ConfirmPayment failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Bookings] ConfirmPayment forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, ex.Message, HttpContext.TraceIdentifier)); }
    }

    [HttpPost("confirm-by-intent")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ConfirmByPaymentIntent([FromBody] ConfirmByIntentRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var booking = await context.BookingViews.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PaymentIntentId == request.PaymentIntentId);

            if (booking is null)
                return NotFound(new ApiError(404, "Payment not found", HttpContext.TraceIdentifier));

            if (booking.UserId != userId)
                return StatusCode(403, new ApiError(403, "Not your booking", HttpContext.TraceIdentifier));

            if (booking.Status != "Pending")
                return Ok(await bookingService.GetByIdAsync(booking.Id));

            var result = await bookingService.ConfirmPaymentAsync(booking.Id, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Bookings] ConfirmByIntent failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Bookings] ConfirmByIntent failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Bookings] ConfirmByIntent forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, ex.Message, HttpContext.TraceIdentifier)); }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var booking = await bookingService.CancelAsync(id, userId);
            return Ok(booking);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Bookings] Cancel failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Bookings] Cancel failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Bookings] Cancel forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, ex.Message, HttpContext.TraceIdentifier)); }
    }

    /// <summary>
    /// Fire-and-forget booking cancellation via navigator.sendBeacon (no auth header possible).
    /// JWT is passed in the request body. Also releases the table lock via sp_cancel_booking.
    /// </summary>
    [HttpPost("cancel-beacon")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelBeacon([FromBody] CancelBeaconRequest request)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtOptions = HttpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>()
                .Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
            var principal = handler.ValidateToken(request.Token, jwtOptions.TokenValidationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            await bookingService.CancelAsync(request.BookingId, userId);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Bookings] Beacon cancel failed for booking {BookingId}", request.BookingId);
            return Ok(); // Still return 200 for beacon (fire-and-forget)
        }
    }

    [HttpPost("{id:guid}/refund")]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> Refund(Guid id)
    {
        var booking = await context.BookingViews.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound(new ApiError(404, "Booking not found", HttpContext.TraceIdentifier));

        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var ev = await context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == booking.EventId);
        if (ev is not null && ev.OrganizerId != adminId && !User.IsInRole(UserRole.Developer.ToString()))
            return StatusCode(403, new ApiError(403, "Not your event", HttpContext.TraceIdentifier));

        try
        {
            var result = await bookingService.RefundAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Bookings] Refund failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Bookings] Refund failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
    }

    [HttpGet("{id:guid}")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var userRole = Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value);
        var booking = await bookingService.GetByIdAsync(id);
        if (booking is null) return NotFound(new ApiError(404, "Booking not found", HttpContext.TraceIdentifier));

        if (userRole < UserRole.Staff && booking.UserId != userId)
            return StatusCode(403, new ApiError(403, "Not your booking", HttpContext.TraceIdentifier));

        return Ok(booking);
    }

    [HttpGet("mine")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var query = context.BookingViews.AsNoTracking()
            .Where(b => b.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b =>
                b.EventTitle.ToLower().Contains(term) ||
                b.BookingNumber.ToLower().Contains(term) ||
                b.Status.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(b => new BookingDto(
            b.Id, b.BookingNumber, b.Status,
            b.UserId, $"{b.UserFirstName} {b.UserLastName}", b.EventId, b.EventTitle,
            b.EventStartDate, b.EventEndDate, b.EventCategory, b.EventImagePath,
            b.VenueName, !string.IsNullOrEmpty(b.VenueAddress) ? $"{b.VenueAddress}, {b.VenueCity}, {b.VenueState}" : null,
            b.SubtotalCents, b.TotalCents, null,
            b.TableId, b.TableLabel, b.SeatsReserved, b.EventTicketTypeId, b.EventTicketTypeLabel, b.TicketCount,
            b.StripeTransactionId.HasValue ? new StripeTransactionDto(
                b.StripeTransactionId.Value, b.PaymentIntentId!, b.PaymentStatus!,
                b.PaymentAmountCents ?? 0, b.TotalChargedCents, b.TaxAmountCents,
                b.StripeFeesCents, b.TransferAmountCents, b.PaidAt, b.RefundedAt
            ) : null,
            b.CreatedAt
        )).ToList();

        return Ok(new PagedResponse<BookingDto>(dtos, total, page, pageSize));
    }

    [HttpGet("stripe-config")]
    [AllowAnonymous]
    public Task<IActionResult> GetStripeConfig()
    {
        var publishableKey = secrets.StripePublishableKey;
        var isLive = !string.IsNullOrEmpty(publishableKey) && publishableKey.StartsWith("pk_live_");
        return Task.FromResult<IActionResult>(Ok(new { publishableKey, mode = isLive ? "live" : "test" }));
    }

    [HttpGet("{id:guid}/qr")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetQrCode(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var png = await bookingService.GetQrImageAsync(id, userId);
            return File(png, "image/png");
        }
        catch (KeyNotFoundException ex) { Log.Warning(ex, "[Bookings] GetQrCode failed: {Message}", ex.Message); return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier)); }
        catch (InvalidOperationException ex) { Log.Warning(ex, "[Bookings] GetQrCode failed: {Message}", ex.Message); return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException ex) { Log.Warning(ex, "[Bookings] GetQrCode forbidden: {Message}", ex.Message); return StatusCode(403, new ApiError(403, ex.Message, HttpContext.TraceIdentifier)); }
    }
}
