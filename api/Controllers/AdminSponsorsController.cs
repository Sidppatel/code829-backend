using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Sponsors;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/sponsors")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminSponsorsController(
    ISponsorService sponsorService,
    IFileStorageService fileStorage
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sponsorService.SearchAsync(q, page, pageSize, includePrivateMeta: true, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var dto = await sponsorService.GetByIdAsync(id, includePrivateMeta: true, ct);
        if (dto is null) return NotFound(new ApiError(404, "Sponsor not found", HttpContext.TraceIdentifier));
        return Ok(dto);
    }

    [HttpGet("slug-check")]
    public async Task<IActionResult> SlugCheck([FromQuery] string slug, [FromQuery] Guid? excludeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new ApiError(400, "slug is required", HttpContext.TraceIdentifier));
        var suggested = await sponsorService.ResolveAvailableSlugAsync(slug, excludeId, ct);
        var available = string.Equals(suggested, slug.Trim().ToLowerInvariant(), StringComparison.Ordinal);
        return Ok(new SlugCheckDto(available, suggested));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSponsorRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiError(400, "Name is required", HttpContext.TraceIdentifier));
        var dto = await sponsorService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, version = "1.0" }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSponsorRequest request, CancellationToken ct = default)
    {
        var dto = await sponsorService.UpdateAsync(id, request, ct);
        if (dto is null) return NotFound(new ApiError(404, "Sponsor not found", HttpContext.TraceIdentifier));
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            var deleted = await sponsorService.DeleteAsync(id, ct);
            if (!deleted) return NotFound(new ApiError(404, "Sponsor not found", HttpContext.TraceIdentifier));
            return NoContent();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("sponsor_linked_to_events") == true)
        {
            return Conflict(new ApiError(409, "Sponsor is linked to events. Remove from all events before deleting.", HttpContext.TraceIdentifier));
        }
        catch (Npgsql.PostgresException ex) when (ex.Message.Contains("sponsor_linked_to_events"))
        {
            return Conflict(new ApiError(409, "Sponsor is linked to events. Remove from all events before deleting.", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiError(400, "No file uploaded", HttpContext.TraceIdentifier));
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new ApiError(400, "File exceeds 5 MB limit", HttpContext.TraceIdentifier));

        var existing = await sponsorService.GetByIdAsync(id, includePrivateMeta: true, ct);
        if (existing is null) return NotFound(new ApiError(404, "Sponsor not found", HttpContext.TraceIdentifier));

        await using var stream = file.OpenReadStream();
        var path = await fileStorage.SaveAsync(stream, "sponsors", file.FileName);
        var updated = await sponsorService.UpdateAsync(id, new UpdateSponsorRequest(PrimaryImagePath: path), ct);
        return Ok(updated);
    }
}
