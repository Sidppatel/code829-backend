using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Tables;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Api.Controllers;

[ApiController]
[Route("tables")]
public class TableBookingController(ITableBookingService tableBookingService) : ControllerBase
{
    [HttpPost("lock")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> LockTable([FromBody] LockTableRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var result = await tableBookingService.LockTableAsync(userId, request.EventId, request.TableId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("release")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ReleaseTable([FromBody] ReleaseTableRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await tableBookingService.ReleaseTableLockAsync(userId, request.EventId, request.TableId);
            return Ok(new { message = "Table released" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Fire-and-forget table release via navigator.sendBeacon (no auth header possible).
    /// JWT is passed as a query parameter instead.
    /// </summary>
    [HttpPost("release-beacon")]
    [AllowAnonymous]
    public async Task<IActionResult> ReleaseTableBeacon(
        [FromBody] ReleaseBeaconRequest request)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(request.Token);
            var userIdClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            await tableBookingService.ReleaseTableLockAsync(userId, request.EventId, request.TableId);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TableBooking] Beacon release failed for table {TableId}", request.EventTableId);
            return Ok(); // Still return 200 for beacon (fire-and-forget) but LOG the error
        }
    }

    [HttpGet("my-locks/{eventId:guid}")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMyLocks(Guid eventId)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var locks = await tableBookingService.GetUserLockedTablesAsync(userId, eventId);
        return Ok(locks);
    }
}
