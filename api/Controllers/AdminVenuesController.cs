using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Venues;
using Contracts.Enums;
using Db;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/venues")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminVenuesController(
    EventPlatformDbContext context,
    IVenueProcedures venueProc,
    IFileStorageService fileStorage
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
            .FirstOrDefaultAsync(v => v.Id == id);
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

        var created = await context.VenueViews.AsNoTracking().FirstAsync(v => v.Id == venueId);
        return CreatedAtAction(nameof(GetById), new { id = venueId }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueRequest request)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        await venueProc.UpdateVenueAsync(
            id, request.Name, request.Description, null,
            request.Phone, request.Email, request.Website, request.IsActive,
            request.Address, request.City, request.State, request.ZipCode);

        var updated = await context.VenueViews.AsNoTracking().FirstAsync(v => v.Id == id);
        return Ok(MapToDto(updated));
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var venue = await context.Venues.FindAsync(id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "venues", file.FileName);
        await venueProc.UpdateVenueAsync(id, null, null, path, null, null, null, null, null, null, null, null);

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        await venueProc.UpdateVenueAsync(id, null, null, null, null, null, null, false, null, null, null, null);
        return NoContent();
    }

    private VenueDto MapToDto(VenueView v) => new(
        v.Id, v.Name, v.AddressLine1, v.City, v.State, v.ZipCode,
        v.Description,
        v.ImagePath is not null ? fileStorage.GetPublicUrl(v.ImagePath) : null,
        v.Phone, v.Email, v.Website, v.IsActive, v.CreatedAt
    );
}
