using Contracts.DTOs;
using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Tables;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/tables")]
public class TableBookingController(ITableBookingService tableBookingService, Db.EventPlatformDbContext context) : ControllerBase
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
            Log.Warning(ex, "[Tables] LockTable not found: {Message}", ex.Message);
            return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "[Tables] LockTable conflict: {Message}", ex.Message);
            return Conflict(new ApiError(409, "Operation not allowed", HttpContext.TraceIdentifier));
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
            Log.Warning(ex, "[Tables] ReleaseTable not found: {Message}", ex.Message);
            return NotFound(new ApiError(404, "Resource not found", HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "[Tables] ReleaseTable failed: {Message}", ex.Message);
            return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("release-beacon")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> ReleaseTableBeacon(
    [FromBody] ReleaseBeaconRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var table = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(context.TableViews, t => t.TableId == request.TableId);
            if (table is null) return Ok();
            if (table.LockedByUserId.HasValue && table.LockedByUserId != userId)
            {
                Log.Warning("[TableBooking] AUDIT beacon_release_ownership_mismatch table={TableId} user={UserId}", request.TableId, userId);
                return Ok();
            }

            await tableBookingService.ReleaseTableLockAsync(userId, request.EventId, request.TableId);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TableBooking] Beacon release failed for table {TableId}", request.EventTableId);
            return Ok();
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
