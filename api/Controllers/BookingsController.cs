using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("bookings")]
public class BookingsController(
    IBookingService bookingService,
    EventPlatformDbContext context,
    ISettingsService settingsService
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
            var payment = await context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PaymentIntentId == request.PaymentIntentId);

            if (payment is null)
                return NotFound(new ApiError(404, "Payment not found", HttpContext.TraceIdentifier));

            if (payment.Booking.UserId != userId)
                return StatusCode(403, new ApiError(403, "Not your booking", HttpContext.TraceIdentifier));

            if (payment.Booking.Status != BookingStatus.Pending)
                return Ok(await bookingService.GetByIdAsync(payment.BookingId));

            var booking = await bookingService.ConfirmPaymentAsync(payment.BookingId, userId);
            return Ok(booking);
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

    [HttpPost("{id:guid}/refund")]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> Refund(Guid id)
    {
        try
        {
            var booking = await bookingService.RefundAsync(id);
            return Ok(booking);
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

        var query = context.Bookings
            .Include(b => b.Event)
            .Include(b => b.Table)
            .Include(b => b.Payment)
            .Include(b => b.User)
            .Where(b => b.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b =>
                b.Event.Title.ToLower().Contains(term) ||
                b.BookingNumber.ToLower().Contains(term) ||
                b.Status.ToString().ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(b => new BookingDto(
            b.Id, b.BookingNumber, b.Status.ToString(),
            b.UserId, b.User.FirstName + " " + b.User.LastName, b.EventId, b.Event.Title,
            b.SubtotalCents, b.FeeCents, b.TotalCents, b.QrToken,
            b.TableId, b.Table?.Label, b.SeatsReserved,
            b.Payment is not null ? new PaymentDto(
                b.Payment.Id, b.Payment.PaymentIntentId, b.Payment.Status.ToString(),
                b.Payment.AmountCents, b.Payment.PaidAt, b.Payment.RefundedAt
            ) : null,
            b.CreatedAt
        )).ToList();

        return Ok(new PagedResponse<BookingDto>(dtos, total, page, pageSize));
    }

    [HttpGet("stripe-config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStripeConfig()
    {
        var publishableKey = await settingsService.GetOrDefaultAsync("stripe_publishable_key", "");
        return Ok(new { publishableKey });
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
