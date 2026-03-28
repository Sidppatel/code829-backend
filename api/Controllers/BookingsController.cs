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

namespace Api.Controllers;

[ApiController]
[Route("bookings")]
[Authorize]
public class BookingsController(
    IBookingService bookingService,
    EventPlatformDbContext context
) : ControllerBase
{
    /// <summary>
    /// Create a new booking (status: Pending).
    /// </summary>
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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Confirm payment for a pending booking (mock auto-pay in dev).
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ConfirmPayment(Guid id)
    {
        try
        {
            var booking = await bookingService.ConfirmPaymentAsync(id);
            return Ok(booking);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Cancel a booking.
    /// </summary>
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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>
    /// Refund a paid booking (Admin only).
    /// </summary>
    [HttpPost("{id:guid}/refund")]
    [RequireRole(UserRole.Admin)]
    public async Task<IActionResult> Refund(Guid id)
    {
        try
        {
            var booking = await bookingService.RefundAsync(id);
            return Ok(booking);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Get a single booking by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var booking = await bookingService.GetByIdAsync(id);
        if (booking is null) return NotFound(new { message = "Booking not found" });
        return Ok(booking);
    }

    /// <summary>
    /// Get current user's bookings.
    /// </summary>
    [HttpGet("mine")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyBookings([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var query = context.Bookings
            .Include(b => b.Event)
            .Include(b => b.Items).ThenInclude(i => i.TicketType)
            .Include(b => b.Payment)
            .Include(b => b.User)
            .Where(b => b.UserId == userId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(b => new BookingDto(
            b.Id, b.BookingNumber, b.Status.ToString(),
            b.UserId, b.User.Name, b.EventId, b.Event.Title,
            b.SubtotalCents, b.FeeCents, b.TotalCents, b.QrToken,
            b.Items.Select(i => new BookingItemDto(
                i.Id, i.TicketTypeId, i.TicketType.Name, i.SeatId, null, i.PriceCents
            )).ToList(),
            b.Payment is not null ? new PaymentDto(
                b.Payment.Id, b.Payment.PaymentIntentId, b.Payment.Status.ToString(),
                b.Payment.AmountCents, b.Payment.PaidAt, b.Payment.RefundedAt
            ) : null,
            b.CreatedAt
        )).ToList();

        return Ok(new PagedResponse<BookingDto>(dtos, total, page, pageSize));
    }

    /// <summary>
    /// Get QR code image for a confirmed booking.
    /// </summary>
    [HttpGet("{id:guid}/qr")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetQrCode(Guid id)
    {
        try
        {
            var png = await bookingService.GetQrImageAsync(id);
            return File(png, "image/png");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
