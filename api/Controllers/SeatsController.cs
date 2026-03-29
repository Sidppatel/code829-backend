using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Seats;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("seats")]
public class SeatsController(ISeatService seatService) : ControllerBase
{
    /// <summary>
    /// Get the seating layout for a venue, optionally showing hold status for an event.
    /// </summary>
    [HttpGet("layout/{venueId:guid}")]
    public async Task<IActionResult> GetLayout(Guid venueId, [FromQuery] Guid? eventId = null)
    {
        try
        {
            var layout = await seatService.GetLayoutAsync(venueId, eventId);
            return Ok(layout);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Hold a seat for an event. Uses SELECT FOR UPDATE to prevent double-holds.
    /// </summary>
    [HttpPost("hold")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> HoldSeat([FromBody] HoldSeatRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var hold = await seatService.HoldSeatAsync(userId, request.EventId, request.SeatId, request.TicketTypeId);
            return Ok(hold);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Release a held seat.
    /// </summary>
    [HttpPost("release")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ReleaseSeat([FromBody] ReleaseSeatRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await seatService.ReleaseSeatAsync(userId, request.EventId, request.SeatId);
            return Ok(new { message = "Seat released" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get current user's active holds for an event.
    /// </summary>
    [HttpGet("holds/{eventId:guid}")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyHolds(Guid eventId)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var holds = await seatService.GetUserHoldsAsync(userId, eventId);
        return Ok(holds);
    }

    /// <summary>
    /// Hold all seats at a table atomically. Uses SELECT FOR UPDATE.
    /// </summary>
    [HttpPost("hold-table")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> HoldTable([FromBody] HoldTableRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var holds = await seatService.HoldTableAsync(userId, request.EventId, request.TableId, request.TicketTypeId);
            return Ok(holds);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Release all seats at a table for the current user.
    /// </summary>
    [HttpPost("release-table")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ReleaseTable([FromBody] ReleaseTableRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await seatService.ReleaseTableAsync(userId, request.EventId, request.TableId);
        return Ok(new { message = "Table released" });
    }
}
