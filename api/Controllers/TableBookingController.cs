using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Tables;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
