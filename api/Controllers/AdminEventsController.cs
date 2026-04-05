using System.Security.Claims;
using System.Text.RegularExpressions;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Events;
using Contracts.DTOs.Venues;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/events")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminEventsController(
    EventPlatformDbContext context,
    IFileStorageService fileStorage,
    IAdminLogService adminLog
) : ControllerBase
{
    [HttpGet]
    public virtual async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.Events.Include(e => e.Venue).ThenInclude(v => v.Address).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EventStatus>(status, true, out var s))
            query = query.Where(e => e.Status == s);

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<EventCategory>(category, true, out var cat))
            query = query.Where(e => e.Category == cat);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term) ||
                e.Slug.ToLower().Contains(term) ||
                e.Venue.Name.ToLower().Contains(term) ||
                (e.Venue.Address != null && e.Venue.Address.City.ToLower().Contains(term))
            );
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(e => MapToDto(e)).ToList();
        return Ok(new PagedResponse<EventDto>(dtos, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ev = await context.Events
            .Include(e => e.Venue).ThenInclude(v => v.Address)
            .Include(e => e.Organizer)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        return Ok(MapToDto(ev));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var venue = await context.Venues.FindAsync(request.VenueId);
        if (venue is null) return BadRequest(new ApiError(400, "Venue not found", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<EventCategory>(request.Category, true, out var category))
            return BadRequest(new ApiError(400, "Invalid category", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<LayoutMode>(request.LayoutMode, true, out var layoutMode))
            return BadRequest(new ApiError(400, "Invalid layout mode", HttpContext.TraceIdentifier));

        var organizerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var slug = GenerateSlug(request.Title);

        var baseSlug = slug;
        var counter = 1;
        while (await context.Events.AnyAsync(e => e.Slug == slug))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Slug = slug,
            Description = request.Description,
            Status = EventStatus.Draft,
            Category = category,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsFeatured = request.IsFeatured,
            LayoutMode = layoutMode,
            MaxCapacity = request.MaxCapacity,
            PricePerPersonCents = layoutMode == LayoutMode.Open ? request.PricePerPersonCents : null,
            PlatformFeePercent = request.PlatformFeePercent,
            ImagePath = request.BannerImageUrl,
            VenueId = request.VenueId,
            OrganizerId = organizerId
        };

        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var created = await context.Events
            .Include(e => e.Venue).ThenInclude(v => v.Address)
            .FirstAsync(e => e.Id == ev.Id);

        await adminLog.LogAsync("event.created", "Event", ev.Id, $"Event '{ev.Title}' created");
        return CreatedAtAction(nameof(GetById), new { id = ev.Id }, MapToDto(created));
    }

    private Guid GetCurrentUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsOwnerOrDeveloper(Guid organizerId) =>
        organizerId == GetCurrentUserId()
        || User.IsInRole(UserRole.Developer.ToString())
        || User.IsInRole(UserRole.Admin.ToString());

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var ev = await context.Events.Include(e => e.Venue).ThenInclude(v => v.Address)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        if (request.Title is not null)
        {
            ev.Title = request.Title;
            ev.Slug = GenerateSlug(request.Title);
        }
        if (request.Description is not null) ev.Description = request.Description;
        if (request.Category is not null && Enum.TryParse<EventCategory>(request.Category, true, out var cat))
            ev.Category = cat;
        if (request.StartDate.HasValue) ev.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) ev.EndDate = request.EndDate.Value;
        if (request.VenueId.HasValue) ev.VenueId = request.VenueId.Value;
        if (request.IsFeatured.HasValue) ev.IsFeatured = request.IsFeatured.Value;
        if (request.LayoutMode is not null && Enum.TryParse<LayoutMode>(request.LayoutMode, true, out var lm))
        {
            if (lm != ev.LayoutMode)
            {
                var hasBookings = await context.Bookings
                    .AnyAsync(b => b.EventId == id && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Refunded);
                if (hasBookings)
                    return BadRequest(new ApiError(400, "Cannot change layout mode — active bookings exist for this event", HttpContext.TraceIdentifier));
            }
            ev.LayoutMode = lm;
        }
        if (request.MaxCapacity.HasValue) ev.MaxCapacity = request.MaxCapacity.Value;
        if (request.PricePerPersonCents.HasValue) ev.PricePerPersonCents = request.PricePerPersonCents.Value;
        if (request.PlatformFeePercent.HasValue) ev.PlatformFeePercent = request.PlatformFeePercent.Value;
        if (request.BannerImageUrl is not null) ev.ImagePath = request.BannerImageUrl;

        if (request.Status is not null && Enum.TryParse<EventStatus>(request.Status, true, out var newStatus))
        {
            if (!IsValidTransition(ev.Status, newStatus))
                return BadRequest(new ApiError(400, $"Cannot transition from {ev.Status} to {newStatus}", HttpContext.TraceIdentifier));
            ev.Status = newStatus;
            if (newStatus == EventStatus.Published) ev.PublishedAt = DateTime.UtcNow;
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapToDto(ev));
    }

    [HttpGet("{id:guid}/layout-locked")]
    public async Task<IActionResult> IsLayoutModeLocked(Guid id)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var hasBookings = await context.Bookings
            .AnyAsync(b => b.EventId == id && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Refunded);

        return Ok(new { locked = hasBookings });
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "events", file.FileName);
        ev.ImagePath = path;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeEventStatusRequest request)
    {
        var ev = await context.Events
            .Include(e => e.Venue).ThenInclude(v => v.Address)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        if (!Enum.TryParse<EventStatus>(request.Status, true, out var newStatus))
            return BadRequest(new ApiError(400, "Invalid status", HttpContext.TraceIdentifier));

        if (!IsValidTransition(ev.Status, newStatus))
            return BadRequest(new ApiError(400, $"Cannot transition from {ev.Status} to {newStatus}", HttpContext.TraceIdentifier));

        if (newStatus == EventStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(ev.Title))
                return BadRequest(new ApiError(400, "Title is required to publish", HttpContext.TraceIdentifier));
            if (ev.StartDate == default || ev.EndDate == default)
                return BadRequest(new ApiError(400, "Dates are required to publish", HttpContext.TraceIdentifier));
        }

        if (newStatus == EventStatus.Completed && ev.EndDate > DateTime.UtcNow)
            return BadRequest(new ApiError(400, "Cannot complete an event before its end date", HttpContext.TraceIdentifier));

        ev.Status = newStatus;
        if (newStatus == EventStatus.Published) ev.PublishedAt = DateTime.UtcNow;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await adminLog.LogAsync($"event.{newStatus.ToString().ToLower()}", "Event", ev.Id,
            $"Event '{ev.Title}' status changed to {newStatus}");
        return Ok(MapToDto(ev));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        if (ev.Status != EventStatus.Draft)
            return BadRequest(new ApiError(400, "Only draft events can be deleted", HttpContext.TraceIdentifier));

        var hasBookings = await context.Bookings.AnyAsync(b => b.EventId == id);
        if (hasBookings)
            return BadRequest(new ApiError(400, "Cannot delete an event with bookings", HttpContext.TraceIdentifier));

        context.Events.Remove(ev);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, [FromBody] DuplicateEventRequest request)
    {
        var original = await context.Events
            .Include(e => e.Venue).ThenInclude(v => v.Address)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (original is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(original.OrganizerId)) return Forbid();

        var organizerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var slug = GenerateSlug(original.Title + " copy");
        var baseSlug = slug;
        var counter = 1;
        while (await context.Events.AnyAsync(e => e.Slug == slug))
            slug = $"{baseSlug}-{counter++}";

        var copy = new Event
        {
            Id = Guid.NewGuid(),
            Title = original.Title + " (Copy)",
            Slug = slug,
            Description = original.Description,
            Status = EventStatus.Draft,
            Category = original.Category,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ImagePath = original.ImagePath,
            IsFeatured = false,
            LayoutMode = original.LayoutMode,
            MaxCapacity = original.MaxCapacity,
            PricePerPersonCents = original.PricePerPersonCents,
            PlatformFeePercent = original.PlatformFeePercent,
            GridRows = original.GridRows,
            GridCols = original.GridCols,
            VenueId = original.VenueId,
            OrganizerId = organizerId
        };
        context.Events.Add(copy);

        // Copy event tables and their table instances
        var eventTables = await context.EventTables
            .Include(et => et.Tables)
            .Where(et => et.EventId == id)
            .ToListAsync();
        foreach (var et in eventTables)
        {
            var newEventTable = new EventTable
            {
                Id = Guid.NewGuid(),
                Label = et.Label,
                Capacity = et.Capacity,
                Shape = et.Shape,
                Color = et.Color,
                PriceCents = et.PriceCents,
                IsActive = et.IsActive,
                EventId = copy.Id,
                TableTemplateId = et.TableTemplateId
            };
            context.EventTables.Add(newEventTable);

            foreach (var t in et.Tables)
            {
                context.Tables.Add(new Table
                {
                    Id = Guid.NewGuid(),
                    Label = t.Label,
                    GridRow = t.GridRow,
                    GridCol = t.GridCol,
                    IsActive = t.IsActive,
                    SortOrder = t.SortOrder,
                    EventTableId = newEventTable.Id,
                    EventId = copy.Id
                });
            }
        }

        await context.SaveChangesAsync();

        await adminLog.LogAsync("event.duplicated", "Event", copy.Id,
            $"Event '{copy.Title}' duplicated from '{original.Title}'");

        var created = await context.Events
            .Include(e => e.Venue).ThenInclude(v => v.Address)
            .FirstAsync(e => e.Id == copy.Id);
        return Created("", MapToDto(created));
    }

    private EventDto MapToDto(Event e) => new(
        e.Id, e.Title, e.Slug, e.Description,
        e.Status.ToString(), (e.Category?.ToString() ?? ""),
        e.StartDate, e.EndDate,
        e.ImagePath is not null ? fileStorage.GetPublicUrl(e.ImagePath) : null,
        e.IsFeatured,
        e.LayoutMode.ToString(), e.MaxCapacity, e.PricePerPersonCents, e.PlatformFeePercent, e.PlatformFeeCents,
        e.GridRows, e.GridCols, e.PublishedAt,
        e.VenueId,
        e.Venue is not null ? new VenueDto(
            e.Venue.Id, e.Venue.Name, e.Venue.Address?.Line1 ?? "", e.Venue.Address?.City ?? "", e.Venue.Address?.State ?? "",
            e.Venue.Address?.ZipCode ?? "", e.Venue.Description,
            e.Venue.ImagePath is not null ? fileStorage.GetPublicUrl(e.Venue.ImagePath) : null,
            e.Venue.Phone, e.Venue.Email, e.Venue.Website,
            e.Venue.IsActive, e.Venue.CreatedAt
        ) : null,
        e.OrganizerId,
        e.Organizer is not null ? $"{e.Organizer.FirstName} {e.Organizer.LastName}" : null,
        e.CreatedAt
    );

    private static bool IsValidTransition(EventStatus current, EventStatus target) => (current, target) switch
    {
        (EventStatus.Draft, EventStatus.Published) => true,
        (EventStatus.Draft, EventStatus.Cancelled) => true,
        (EventStatus.Published, EventStatus.Completed) => true,
        (EventStatus.Published, EventStatus.Cancelled) => true,
        _ => false
    };

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }
}
