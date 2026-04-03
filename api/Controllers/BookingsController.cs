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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var userRole = Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value);
        var booking = await bookingService.GetByIdAsync(id);
        if (booking is null) return NotFound(new { message = "Booking not found" });

        if (userRole < UserRole.Staff && booking.UserId != userId)
            return Forbid("Not your booking");

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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
}
