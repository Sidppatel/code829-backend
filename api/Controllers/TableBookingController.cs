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

[ApiController]
[Route("tables")]
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
            return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(409, ex.Message, HttpContext.TraceIdentifier));
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
            return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier));
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
            var jwtOptions = HttpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>()
                .Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
            var principal = handler.ValidateToken(request.Token, jwtOptions.TokenValidationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            // Explicit ownership re-check at the controller boundary. ReleaseTableLockAsync
            // also checks but we verify up-front so a misbehaving client can't probe lock state.
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
