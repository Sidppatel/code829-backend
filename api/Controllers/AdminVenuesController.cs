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
[IgnoreAntiforgeryToken]
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

        var query = context.Venues.Include(v => v.Address).AsQueryable();
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
        var venue = await context.Venues.Include(v => v.Address).FirstOrDefaultAsync(v => v.Id == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));
        return Ok(MapToDto(venue, fileStorage));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest request)
    {
        var address = new Address
        {
            Id = Guid.NewGuid(),
            Line1 = request.Address,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode
        };
        context.Addresses.Add(address);

        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            AddressId = address.Id,
            Address = address,
            Description = request.Description,
            Phone = request.Phone,
            Email = request.Email,
            Website = request.Website
        };

        context.Venues.Add(venue);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = venue.Id }, MapToDto(venue, fileStorage));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueRequest request)
    {
        var venue = await context.Venues.Include(v => v.Address).FirstOrDefaultAsync(v => v.Id == id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        if (request.Name is not null) venue.Name = request.Name;
        if (request.Description is not null) venue.Description = request.Description;
        if (request.Phone is not null) venue.Phone = request.Phone;
        if (request.Email is not null) venue.Email = request.Email;
        if (request.Website is not null) venue.Website = request.Website;
        if (request.IsActive.HasValue) venue.IsActive = request.IsActive.Value;

        // Update address fields
        if (request.Address is not null || request.City is not null || request.State is not null || request.ZipCode is not null)
        {
            if (venue.Address is null)
            {
                var address = new Address
                {
                    Id = Guid.NewGuid(),
                    Line1 = request.Address ?? "",
                    City = request.City ?? "",
                    State = request.State ?? "",
                    ZipCode = request.ZipCode ?? ""
                };
                context.Addresses.Add(address);
                venue.AddressId = address.Id;
                venue.Address = address;
            }
            else
            {
                if (request.Address is not null) venue.Address.Line1 = request.Address;
                if (request.City is not null) venue.Address.City = request.City;
                if (request.State is not null) venue.Address.State = request.State;
                if (request.ZipCode is not null) venue.Address.ZipCode = request.ZipCode;
            }
        }

        venue.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapToDto(venue, fileStorage));
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var venue = await context.Venues.FindAsync(id);
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

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
        if (venue is null) return NotFound(new ApiError(404, "Venue not found", HttpContext.TraceIdentifier));

        venue.IsActive = false;
        venue.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static VenueDto MapToDto(Venue v, IFileStorageService fs) => new(
        v.Id, v.Name, v.Address?.Line1 ?? "", v.Address?.City ?? "", v.Address?.State ?? "", v.Address?.ZipCode ?? "",
        v.Description,
        v.ImagePath is not null ? fs.GetPublicUrl(v.ImagePath) : null,
        v.Phone, v.Email, v.Website, v.IsActive, v.CreatedAt
    );
}
