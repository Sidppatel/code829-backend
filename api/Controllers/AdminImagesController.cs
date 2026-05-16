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

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/images")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminImagesController(
    EventPlatformDbContext context,
    IImageService imageService,
    IAdminLogService adminLog
) : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
    IFormFile file,
    [FromQuery] string entityType,
    [FromQuery] Guid entityId)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        if (entityType is not ("venue" or "event"))
            return BadRequest(new ApiError(400, "entityType must be 'venue' or 'event'", HttpContext.TraceIdentifier));

        var userId = GetCurrentUserId();
        if (!await CanManageEntityAsync(entityType, entityId, userId))
            return NotFound(new ApiError(404, $"{entityType} not found or access denied", HttpContext.TraceIdentifier));

        var adminId = GetCurrentUserId();
        var result = await imageService.UploadAsync(file.OpenReadStream(), file.FileName, entityType, entityId, uploadedById: adminId, uploaderType: "admin");
        await adminLog.LogAsync("UploadImage", entityType, entityId, $"Uploaded image for {entityType} {entityId}");

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByEntity([FromQuery] string entityType, [FromQuery] Guid entityId)
    {
        var images = await imageService.GetByEntityAsync(entityType, entityId);
        return Ok(images);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await imageService.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiError(404, "Image not found", HttpContext.TraceIdentifier));

        await adminLog.LogAsync("DeleteImage", "image", id, $"Deleted image {id}");
        return NoContent();
    }

    [HttpPatch("{id:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid id)
    {
        await imageService.SetPrimaryAsync(id);
        return Ok(new { message = "Image set as primary" });
    }

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

        if (User.IsInRole(UserRole.Developer.ToString()) || User.IsInRole(UserRole.Admin.ToString()))
            return true;

        return entityType switch
        {
            "venue" => await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == entityId),
            "event" => await context.EventViews.AsNoTracking().AnyAsync(e => e.EventId == entityId && e.BusinessUserId == userId),
            _ => false
        };
    }
}
