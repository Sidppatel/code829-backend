using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Images;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/images")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminImagesController(
    EventPlatformDbContext context,
    IImageService imageService,
    IAdminLogService adminLog
) : ControllerBase
{
    /// <summary>
    /// Upload an image for a venue or event.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string entityType,
        [FromQuery] Guid entityId)
    {
        if (entityType is not ("venue" or "event"))
            return BadRequest(new ApiError(400, "entityType must be 'venue' or 'event'", HttpContext.TraceIdentifier));

        // Verify entity exists and user has access
        var userId = GetCurrentUserId();
        if (!await CanManageEntityAsync(entityType, entityId, userId))
            return NotFound(new ApiError(404, $"{entityType} not found or access denied", HttpContext.TraceIdentifier));

        var result = await imageService.UploadAsync(file.OpenReadStream(), file.FileName, entityType, entityId, userId);
        await adminLog.LogAsync("UploadImage", entityType, entityId, $"Uploaded image for {entityType} {entityId}");

        return Ok(result);
    }

    /// <summary>
    /// Get all images for a venue or event.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetByEntity([FromQuery] string entityType, [FromQuery] Guid entityId)
    {
        var images = await imageService.GetByEntityAsync(entityType, entityId);
        return Ok(images);
    }

    /// <summary>
    /// Delete an image.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await imageService.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiError(404, "Image not found", HttpContext.TraceIdentifier));

        await adminLog.LogAsync("DeleteImage", "image", id, $"Deleted image {id}");
        return NoContent();
    }

    /// <summary>
    /// Set an image as primary.
    /// </summary>
    [HttpPatch("{id:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid id)
    {
        await imageService.SetPrimaryAsync(id);
        return Ok(new { message = "Image set as primary" });
    }

    /// <summary>
    /// Reorder images for an entity.
    /// </summary>
    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromBody] ReorderImagesRequest request)
    {
        await imageService.ReorderAsync(entityType, entityId, request.ImageIds);
        return Ok(new { message = "Images reordered" });
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(claim!);
    }

    private async Task<bool> CanManageEntityAsync(string entityType, Guid entityId, Guid userId)
    {
        var user = await context.Users.FindAsync(userId);
        if (user is null) return false;
        if (user.Role == UserRole.Developer || user.Role == UserRole.Admin) return true;

        return entityType switch
        {
            "venue" => await context.Venues.AnyAsync(v => v.Id == entityId),
            "event" => await context.Events.AnyAsync(e => e.Id == entityId && e.OrganizerId == userId),
            _ => false
        };
    }
}
