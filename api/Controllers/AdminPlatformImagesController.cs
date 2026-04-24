using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Images;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/platform-images")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminPlatformImagesController(
    IPlatformImageService platformImageService
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Add(
        IFormFile file,
        [FromForm] string? tag = null,
        [FromForm] string? altText = null,
        [FromForm] string? caption = null)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var adminId = GetCurrentUserId();
        var result = await platformImageService.AddAsync(file.OpenReadStream(), file.FileName, adminId, tag, altText, caption);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tag = null)
    {
        var list = await platformImageService.ListAsync(tag);
        return Ok(list);
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder([FromBody] ReorderPlatformImagesRequest request)
    {
        if (request.ImageIds is null || request.ImageIds.Count == 0)
            return BadRequest(new ApiError(400, "imageIds required", HttpContext.TraceIdentifier));
        if (request.ImageIds.Distinct().Count() != request.ImageIds.Count)
            return BadRequest(new ApiError(400, "imageIds must be unique", HttpContext.TraceIdentifier));

        await platformImageService.ReorderAsync(request.ImageIds);
        return NoContent();
    }

    [HttpPut("{imageId:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid imageId)
    {
        var ok = await platformImageService.SetPrimaryAsync(imageId);
        if (!ok) return NotFound(new ApiError(404, "Platform image not found", HttpContext.TraceIdentifier));
        return NoContent();
    }

    [HttpDelete("{imageId:guid}")]
    public async Task<IActionResult> Delete(Guid imageId)
    {
        var ok = await platformImageService.RemoveAsync(imageId);
        if (!ok) return NotFound(new ApiError(404, "Platform image not found", HttpContext.TraceIdentifier));
        return NoContent();
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
