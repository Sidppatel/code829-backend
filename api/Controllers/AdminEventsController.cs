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

        var query = context.Events.Include(e => e.Venue).Include(e => e.TicketTypes).AsQueryable();

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
                e.Venue.City.ToLower().Contains(term)
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
            .Include(e => e.Venue)
            .Include(e => e.Organizer)
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound(new { message = "Event not found" });
        return Ok(MapToDto(ev));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var venue = await context.Venues.FindAsync(request.VenueId);
        if (venue is null) return BadRequest(new { message = "Venue not found" });

        if (!Enum.TryParse<EventCategory>(request.Category, true, out var category))
            return BadRequest(new { message = "Invalid category" });

        var organizerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var slug = GenerateSlug(request.Title);

        // Ensure slug uniqueness
        var baseSlug = slug;
        var counter = 1;
        while (await context.Events.AnyAsync(e => e.Slug == slug))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        var layoutMode = Contracts.Enums.LayoutMode.None;
        if (request.LayoutMode is not null)
            Enum.TryParse(request.LayoutMode, true, out layoutMode);

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
            ImagePath = request.BannerImageUrl,
            VenueId = request.VenueId,
            OrganizerId = organizerId
        };

        context.Events.Add(ev);

        if (request.TicketTypes is not null)
        {
            foreach (var tt in request.TicketTypes)
            {
                context.TicketTypes.Add(new TicketType
                {
                    Id = Guid.NewGuid(),
                    Name = tt.Name,
                    Description = tt.Description,
                    PriceCents = tt.PriceCents,
                    QuantityTotal = tt.QuantityTotal,
                    QuantitySold = 0,
                    SortOrder = tt.SortOrder,
                    PlatformFeeCents = 0, // Admin cannot set fees
                    EventId = ev.Id
                });
            }
        }

        await context.SaveChangesAsync();

        var created = await context.Events
            .Include(e => e.Venue).Include(e => e.TicketTypes)
            .FirstAsync(e => e.Id == ev.Id);

        await adminLog.LogAsync("event.created", "Event", ev.Id, $"Event '{ev.Title}' created");
        return CreatedAtAction(nameof(GetById), new { id = ev.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var ev = await context.Events.Include(e => e.Venue).Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new { message = "Event not found" });

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
        if (request.LayoutMode is not null && Enum.TryParse<Contracts.Enums.LayoutMode>(request.LayoutMode, true, out var lm))
            ev.LayoutMode = lm;
        if (request.MaxCapacity.HasValue) ev.MaxCapacity = request.MaxCapacity.Value;
        if (request.BannerImageUrl is not null) ev.ImagePath = request.BannerImageUrl;

        // Status transitions: Draft→Published, Published→Completed/Cancelled, Draft→Cancelled
        if (request.Status is not null && Enum.TryParse<EventStatus>(request.Status, true, out var newStatus))
        {
            if (!IsValidTransition(ev.Status, newStatus))
                return BadRequest(new { message = $"Cannot transition from {ev.Status} to {newStatus}" });
            ev.Status = newStatus;
            if (newStatus == EventStatus.Published) ev.PublishedAt = DateTime.UtcNow;
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapToDto(ev));
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "events", file.FileName);
        ev.ImagePath = path;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    /// <summary>
    /// Change event status with validation gates.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeEventStatusRequest request)
    {
        var ev = await context.Events
            .Include(e => e.Venue).Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new { message = "Event not found" });

        if (!Enum.TryParse<EventStatus>(request.Status, true, out var newStatus))
            return BadRequest(new { message = "Invalid status" });

        if (!IsValidTransition(ev.Status, newStatus))
            return BadRequest(new { message = $"Cannot transition from {ev.Status} to {newStatus}" });

        // Validation gates
        if (newStatus == EventStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(ev.Title))
                return BadRequest(new { message = "Title is required to publish" });
            if (ev.StartDate == default || ev.EndDate == default)
                return BadRequest(new { message = "Dates are required to publish" });
        }

        if (newStatus == EventStatus.Completed && ev.EndDate > DateTime.UtcNow)
            return BadRequest(new { message = "Cannot complete an event before its end date" });

        ev.Status = newStatus;
        if (newStatus == EventStatus.Published) ev.PublishedAt = DateTime.UtcNow;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await adminLog.LogAsync($"event.{newStatus.ToString().ToLower()}", "Event", ev.Id,
            $"Event '{ev.Title}' status changed to {newStatus}");
        return Ok(MapToDto(ev));
    }

    /// <summary>
    /// Soft delete — only if Draft and no bookings.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new { message = "Event not found" });

        if (ev.Status != EventStatus.Draft)
            return BadRequest(new { message = "Only draft events can be deleted" });

        var hasBookings = await context.Bookings.AnyAsync(b => b.EventId == id);
        if (hasBookings)
            return BadRequest(new { message = "Cannot delete an event with bookings" });

        context.Events.Remove(ev);
        await context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Duplicate an event: copies settings, layout tables, and pricing rules.
    /// Resets status to Draft, clears bookings/holds, requires new dates.
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, [FromBody] DuplicateEventRequest request)
    {
        var original = await context.Events
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (original is null) return NotFound(new { message = "Event not found" });

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
            EditorMode = original.EditorMode,
            GridRows = original.GridRows,
            GridCols = original.GridCols,
            VenueId = original.VenueId,
            OrganizerId = organizerId
        };
        context.Events.Add(copy);

        // Copy layout tables
        var tables = await context.Tables.Where(t => t.EventId == id).ToListAsync();
        foreach (var t in tables)
        {
            context.Tables.Add(new Table
            {
                Id = Guid.NewGuid(), Label = t.Label, Capacity = t.Capacity, Shape = t.Shape,
                Color = t.Color, Section = t.Section, PriceType = t.PriceType, PriceCents = t.PriceCents,
                PriceOverrideCents = t.PriceOverrideCents, IsActive = t.IsActive,
                GridRow = t.GridRow, GridCol = t.GridCol,
                SortOrder = t.SortOrder,
                PlatformFeeCents = 0, // Admin cannot set fees
                TableTypeId = t.TableTypeId, EventId = copy.Id, VenueId = copy.VenueId
            });
        }

        // Copy pricing rules (reset UsedCount)
        var rules = await context.PricingRules.Where(r => r.EventId == id).ToListAsync();
        foreach (var r in rules)
        {
            context.PricingRules.Add(new PricingRule
            {
                Id = Guid.NewGuid(), EventId = copy.Id, TableTypeId = r.TableTypeId,
                Name = r.Name, Type = r.Type, PriceCents = r.PriceCents,
                ValidFrom = r.ValidFrom, ValidUntil = r.ValidUntil,
                MaxCount = r.MaxCount, UsedCount = 0, IsActive = r.IsActive,
                SortOrder = r.SortOrder, FeePercent = null,
                FeeFlatCents = null, Description = r.Description
            });
        }

        await context.SaveChangesAsync();

        await adminLog.LogAsync("event.duplicated", "Event", copy.Id,
            $"Event '{copy.Title}' duplicated from '{original.Title}'");

        var created = await context.Events
            .Include(e => e.Venue).Include(e => e.TicketTypes)
            .FirstAsync(e => e.Id == copy.Id);
        return Created("", MapToDto(created));
    }

    private EventDto MapToDto(Event e) => new(
        e.Id, e.Title, e.Slug, e.Description,
        e.Status.ToString(), (e.Category?.ToString() ?? ""),
        e.StartDate, e.EndDate,
        e.ImagePath is not null ? fileStorage.GetPublicUrl(e.ImagePath) : null,
        e.IsFeatured,
        (e.LayoutMode?.ToString() ?? "None"), e.MaxCapacity, e.PlatformFeePercent, e.PublishedAt,
        e.VenueId,
        e.Venue is not null ? new VenueDto(
            e.Venue.Id, e.Venue.Name, e.Venue.Address, e.Venue.City, e.Venue.State,
            e.Venue.ZipCode, e.Venue.Description,
            e.Venue.ImagePath is not null ? fileStorage.GetPublicUrl(e.Venue.ImagePath) : null,
            e.Venue.Phone, e.Venue.Website,
            e.Venue.IsActive, e.Venue.CreatedAt
        ) : null,
        e.OrganizerId,
        e.Organizer?.Name,
        e.TicketTypes.OrderBy(t => t.SortOrder).Select(t => new TicketTypeDto(
            t.Id, t.Name ?? "", t.Description, t.PriceCents ?? 0,
            t.QuantityTotal, t.QuantitySold, t.QuantityTotal - t.QuantitySold, t.SortOrder,
            t.PlatformFeeCents ?? 0
        )).ToList(),
        e.CreatedAt
    );

    /// <summary>
    /// Valid lifecycle transitions: Draft→Published, Draft→Cancelled, Published→Completed, Published→Cancelled.
    /// </summary>
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
