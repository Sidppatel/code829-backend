using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Venues;
using Contracts.Enums;
using Db;
using Db.Entities;
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
    IFileStorageService fileStorage
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.Venues.AsQueryable();
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => MapToDto(v, fileStorage))
            .ToListAsync();

        return Ok(new PagedResponse<VenueDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var venue = await context.Venues.FindAsync(id);
        if (venue is null) return NotFound(new { message = "Venue not found" });
        return Ok(MapToDto(venue, fileStorage));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest request)
    {
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            Capacity = request.Capacity,
            Description = request.Description,
            Phone = request.Phone,
            Website = request.Website,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        context.Venues.Add(venue);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = venue.Id }, MapToDto(venue, fileStorage));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueRequest request)
    {
        var venue = await context.Venues.FindAsync(id);
        if (venue is null) return NotFound(new { message = "Venue not found" });

        if (request.Name is not null) venue.Name = request.Name;
        if (request.Address is not null) venue.Address = request.Address;
        if (request.City is not null) venue.City = request.City;
        if (request.State is not null) venue.State = request.State;
        if (request.ZipCode is not null) venue.ZipCode = request.ZipCode;
        if (request.Capacity.HasValue) venue.Capacity = request.Capacity.Value;
        if (request.Description is not null) venue.Description = request.Description;
        if (request.Phone is not null) venue.Phone = request.Phone;
        if (request.Website is not null) venue.Website = request.Website;
        if (request.Latitude.HasValue) venue.Latitude = request.Latitude.Value;
        if (request.Longitude.HasValue) venue.Longitude = request.Longitude.Value;
        if (request.IsActive.HasValue) venue.IsActive = request.IsActive.Value;

        venue.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapToDto(venue, fileStorage));
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var venue = await context.Venues.FindAsync(id);
        if (venue is null) return NotFound(new { message = "Venue not found" });

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "venues", file.FileName);
        venue.ImagePath = path;
        venue.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var venue = await context.Venues.FindAsync(id);
        if (venue is null) return NotFound(new { message = "Venue not found" });

        venue.IsActive = false;
        venue.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static VenueDto MapToDto(Venue v, IFileStorageService fs) => new(
        v.Id, v.Name, v.Address, v.City, v.State, v.ZipCode,
        v.Capacity, v.Description,
        v.ImagePath is not null ? fs.GetPublicUrl(v.ImagePath) : null,
        v.Phone, v.Website, v.Latitude, v.Longitude, v.IsActive, v.CreatedAt
    );
}
