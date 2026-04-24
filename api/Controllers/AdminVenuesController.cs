using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Images;
using Contracts.DTOs.Venues;
using Contracts.Enums;
using Db;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/venues")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminVenuesController(
    EventPlatformDbContext context,
    IVenueProcedures venueProc,
    IFileStorageService fileStorage,
    IVenueImageService venueImageService
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.VenueViews.AsNoTracking().AsQueryable();
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(v => MapToDto(v)).ToList();
        return Ok(new PagedResponse<VenueDto>(dtos, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VenueId == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));
        return Ok(MapToDto(venue));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest request)
    {
        var venueId = await venueProc.CreateVenueAsync(
            request.Name, request.Description, null,
            request.Phone, request.Email, request.Website,
            request.Address, null, request.City, request.State, request.ZipCode);

        var created = await context.VenueViews.AsNoTracking().FirstAsync(v => v.VenueId == venueId);
        return CreatedAtAction(nameof(GetById), new { id = venueId }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueRequest request)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VenueId == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        await venueProc.UpdateVenueAsync(
            id, request.Name, request.Description, null,
            request.Phone, request.Email, request.Website, request.IsActive,
            request.Address, request.City, request.State, request.ZipCode);

        var updated = await context.VenueViews.AsNoTracking().FirstAsync(v => v.VenueId == id);
        return Ok(MapToDto(updated));
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var venueExists = await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == id);
        if (!venueExists) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "venues", file.FileName);
        await venueProc.UpdateVenueAsync(id, null, null, path, null, null, null, null, null, null, null, null);

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VenueId == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        await venueProc.UpdateVenueAsync(id, null, null, null, null, null, null, false, null, null, null, null);
        return NoContent();
    }

    [HttpPost("{venueId:guid}/images")]
    public async Task<IActionResult> AddVenueImage(Guid venueId, IFormFile file, [FromForm] string? altText = null, [FromForm] string? caption = null)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var venueExists = await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == venueId);
        if (!venueExists) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        var adminId = GetCurrentUserId();
        var result = await venueImageService.AddAsync(venueId, file.OpenReadStream(), file.FileName, adminId, altText, caption);
        return Ok(result);
    }

    [HttpGet("{venueId:guid}/images")]
    public async Task<IActionResult> ListVenueImages(Guid venueId)
    {
        var venueExists = await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == venueId);
        if (!venueExists) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        var list = await venueImageService.ListAsync(venueId);
        return Ok(list);
    }

    [HttpPut("{venueId:guid}/images/order")]
    public async Task<IActionResult> ReorderVenueImages(Guid venueId, [FromBody] ReorderVenueImagesRequest request)
    {
        if (request.ImageIds is null || request.ImageIds.Count == 0)
            return BadRequest(new ApiError(400, "imageIds required", HttpContext.TraceIdentifier));
        if (request.ImageIds.Distinct().Count() != request.ImageIds.Count)
            return BadRequest(new ApiError(400, "imageIds must be unique", HttpContext.TraceIdentifier));

        var venueExists = await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == venueId);
        if (!venueExists) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        await venueImageService.ReorderAsync(venueId, request.ImageIds);
        return NoContent();
    }

    [HttpPut("{venueId:guid}/images/{imageId:guid}/primary")]
    public async Task<IActionResult> SetVenueImagePrimary(Guid venueId, Guid imageId)
    {
        var venueExists = await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == venueId);
        if (!venueExists) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        var ok = await venueImageService.SetPrimaryAsync(venueId, imageId);
        if (!ok) return NotFound(new ApiError(404, "Image not found on this venue", HttpContext.TraceIdentifier));
        return NoContent();
    }

    [HttpDelete("{venueId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteVenueImage(Guid venueId, Guid imageId)
    {
        var venueExists = await context.VenueViews.AsNoTracking().AnyAsync(v => v.VenueId == venueId);
        if (!venueExists) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        var ok = await venueImageService.RemoveAsync(venueId, imageId);
        if (!ok) return NotFound(new ApiError(404, "Image not found on this venue", HttpContext.TraceIdentifier));
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claim = System.Security.Claims.ClaimTypes.NameIdentifier;
        return Guid.Parse(User.FindFirst(claim)!.Value);
    }

    private VenueDto MapToDto(VenueView v) => new(
        v.VenueId, v.Name, v.AddressLine1, v.City, v.State, v.ZipCode,
        v.Description,
        v.ImagePath is not null ? fileStorage.GetPublicUrl(v.ImagePath) : null,
        v.Phone, v.Email, v.Website, v.IsActive, v.CreatedAt
    );
}
