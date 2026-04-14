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
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
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
    IEventProcedures eventProc,
    ITableProcedures tableProc,
    IEventTicketTypeProcedures ticketTypeProc,
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

        var query = context.EventViews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term) ||
                e.Slug.ToLower().Contains(term) ||
                e.VenueName.ToLower().Contains(term) ||
                e.VenueCity.ToLower().Contains(term)
            );
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return Ok(new PagedResponse<EventDto>(dtos, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        
        var dto = MapToDto(ev);

        if (ev.LayoutMode == "Open")
        {
            var ticketTypeViews = await context.EventTicketTypeSummaryViews.AsNoTracking()
                .Where(tt => tt.EventId == id && tt.IsActive)
                .OrderBy(tt => tt.SortOrder)
                .ToListAsync();

            var tiers = ticketTypeViews.Select(tt => new EventPricingTierDto(
                tt.Label,
                tt.PriceCents,
                tt.MaxQuantity,
                tt.MaxQuantity ?? -1,
                tt.SoldCount,
                tt.MaxQuantity
            )).ToList();
            
            // If no tiers exist but there's a base price, add a default tier
            if (tiers.Count == 0 && ev.PricePerPersonCents > 0)
            {
                tiers.Add(new EventPricingTierDto("Standard Entry", ev.PricePerPersonCents.Value, ev.MaxCapacity, ev.MaxCapacity ?? 0, ev.TotalSold, ev.MaxCapacity));
            }

            dto = dto with { 
                PricingTiers = tiers,
                TicketTypes = ticketTypeViews.Select(tt => new EventTicketTypeDto(
                    tt.Id, tt.Label, tt.PriceCents, tt.PlatformFeeCents,
                    tt.MaxQuantity, tt.SortOrder, tt.IsActive,
                    tt.SoldCount, tt.AvailableCount)).ToList()
            };
        }
        else if (ev.LayoutMode == "Grid")
        {
            var tiers = await context.EventTablesSummaryViews.AsNoTracking()
                .Where(et => et.EventId == id)
                .OrderBy(et => et.Label)
                .Select(et => new EventPricingTierDto(
                    et.Label,
                    et.PriceCents,
                    et.Capacity,
                    et.TotalTables,
                    et.BookedTables,
                    et.Capacity * et.TotalTables
                ))
                .ToListAsync();
            
            dto = dto with { PricingTiers = tiers };
        }

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VenueId);
        if (venue is null) return BadRequest(new ApiError(400, "Venue not found", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<EventCategory>(request.Category, true, out _))
            return BadRequest(new ApiError(400, "Invalid category", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<LayoutMode>(request.LayoutMode, true, out var layoutMode))
            return BadRequest(new ApiError(400, "Invalid layout mode", HttpContext.TraceIdentifier));

        var organizerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var slug = GenerateSlug(request.Title);

        var baseSlug = slug;
        var counter = 1;
        while (await context.Events.AnyAsync(e => e.Slug == slug))
            slug = $"{baseSlug}-{counter++}";

        var eventId = await eventProc.CreateEventAsync(
            request.Title, slug, request.Description, "Draft", request.Category,
            request.StartDate, request.EndDate, request.BannerImageUrl, request.IsFeatured,
            request.LayoutMode, request.MaxCapacity,
            layoutMode == LayoutMode.Open ? request.PricePerPersonCents : null,
            request.PlatformFeePercent, null, null, null,
            request.VenueId, organizerId, null);

        await adminLog.LogAsync("event.created", "Event", eventId, $"Event '{request.Title}' created");
        
        if (layoutMode == LayoutMode.Open && request.TicketTypes != null)
        {
            var sortOrder = 0;
            foreach (var tt in request.TicketTypes)
            {
                await ticketTypeProc.CreateAsync(eventId, tt.Name, tt.PriceCents, null, tt.Capacity, sortOrder++);
            }
        }


        var created = await context.EventViews.AsNoTracking().FirstAsync(e => e.Id == eventId);
        return CreatedAtAction(nameof(GetById), new { id = eventId }, MapToDto(created));
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
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        string? newSlug = null;
        if (request.Title is not null)
            newSlug = GenerateSlug(request.Title);

        string? newStatus = null;
        if (request.Status is not null && Enum.TryParse<EventStatus>(request.Status, true, out var newS))
        {
            if (!IsValidTransition(Enum.Parse<EventStatus>(ev.Status), newS))
                return BadRequest(new ApiError(400, $"Cannot transition from {ev.Status} to {newS}", HttpContext.TraceIdentifier));
            newStatus = newS.ToString();
        }

        if (request.LayoutMode is not null && Enum.TryParse<LayoutMode>(request.LayoutMode, true, out var lm))
        {
            if (lm.ToString() != ev.LayoutMode)
            {
                var hasBookings = await context.BookingViews.AsNoTracking()
                    .AnyAsync(b => b.EventId == id && b.Status != "Cancelled" && b.Status != "Refunded");
                if (hasBookings)
                    return BadRequest(new ApiError(400, "Cannot change layout mode — active bookings exist for this event", HttpContext.TraceIdentifier));
            }
        }

        await eventProc.UpdateEventAsync(
            id, request.Title, newSlug, request.Description, request.Category,
            request.StartDate, request.EndDate, request.BannerImageUrl, request.IsFeatured,
            request.LayoutMode, request.MaxCapacity, request.PricePerPersonCents,
            request.PlatformFeePercent, null, null, null, request.VenueId, null);

        if (newStatus is not null)
            await eventProc.ChangeEventStatusAsync(id, newStatus, null);

        // Sync Ticket Types (Pricing Tiers) for Open events
        if (request.TicketTypes != null && (request.LayoutMode == "Open" || (request.LayoutMode == null && ev.LayoutMode == "Open")))
        {
            var existingTiers = await context.EventTicketTypes
                .Where(tt => tt.EventId == id && tt.IsActive)
                .ToListAsync();

            var requestIds = request.TicketTypes.Where(t => t.Id.HasValue).Select(t => t.Id!.Value).ToList();
            
            // Delete tiers not in request
            var toDelete = existingTiers.Where(et => !requestIds.Contains(et.Id)).ToList();
            foreach (var td in toDelete)
            {
                await ticketTypeProc.DeleteAsync(td.Id);
            }

            // Upsert remaining
            var sortOrder = 0;
            foreach (var tt in request.TicketTypes)
            {
                if (tt.Id.HasValue && existingTiers.Any(et => et.Id == tt.Id.Value))
                {
                    await ticketTypeProc.UpdateAsync(tt.Id.Value, tt.Name, tt.PriceCents, null, tt.Capacity, sortOrder++, true);
                }
                else
                {
                    await ticketTypeProc.CreateAsync(id, tt.Name, tt.PriceCents, null, tt.Capacity, sortOrder++);
                }
            }
        }

        var updated = await context.EventViews.AsNoTracking().FirstAsync(e => e.Id == id);
        return Ok(MapToDto(updated));
    }

    [HttpGet("{id:guid}/layout-locked")]
    public async Task<IActionResult> IsLayoutModeLocked(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var hasBookings = await context.BookingViews.AsNoTracking()
            .AnyAsync(b => b.EventId == id && b.Status != "Cancelled" && b.Status != "Refunded");

        return Ok(new { locked = hasBookings });
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "events", file.FileName);
        await eventProc.UpdateEventAsync(id, null, null, null, null, null, null, path,
            null, null, null, null, null, null, null, null, null, null);

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeEventStatusRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        if (!Enum.TryParse<EventStatus>(request.Status, true, out var newStatus))
            return BadRequest(new ApiError(400, "Invalid status", HttpContext.TraceIdentifier));

        if (!IsValidTransition(Enum.Parse<EventStatus>(ev.Status), newStatus))
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

        await eventProc.ChangeEventStatusAsync(id, newStatus.ToString(), null);

        await adminLog.LogAsync($"event.{newStatus.ToString().ToLower()}", "Event", id,
            $"Event '{ev.Title}' status changed to {newStatus}");

        var updated = await context.EventViews.AsNoTracking().FirstAsync(e => e.Id == id);
        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await context.Events.FindAsync(id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        if (ev.Status != EventStatus.Draft)
            return BadRequest(new ApiError(400, "Only draft events can be deleted", HttpContext.TraceIdentifier));

        var hasBookings = await context.BookingViews.AsNoTracking().AnyAsync(b => b.EventId == id);
        if (hasBookings)
            return BadRequest(new ApiError(400, "Cannot delete an event with bookings", HttpContext.TraceIdentifier));

        context.Events.Remove(ev);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, [FromBody] DuplicateEventRequest request)
    {
        var original = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (original is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(original.OrganizerId)) return Forbid();

        var organizerId = GetCurrentUserId();
        var slug = GenerateSlug(original.Title + " copy");
        var baseSlug = slug;
        var counter = 1;
        while (await context.Events.AnyAsync(e => e.Slug == slug))
            slug = $"{baseSlug}-{counter++}";

        var copyId = await eventProc.CreateEventAsync(
            original.Title + " (Copy)", slug, original.Description, "Draft", original.Category,
            request.StartDate, request.EndDate, original.ImagePath, false,
            original.LayoutMode, original.MaxCapacity, original.PricePerPersonCents,
            original.PlatformFeePercent, original.PlatformFeeCents,
            original.GridRows, original.GridCols, original.VenueId, organizerId, null);

        // Copy event tables and their table instances
        var eventTables = await context.EventTables
            .Include(et => et.Tables)
            .Where(et => et.EventId == id)
            .ToListAsync();
        foreach (var et in eventTables)
        {
            var newEtId = await tableProc.CreateEventTableAsync(
                copyId, et.Label, et.Capacity, et.Shape.ToString(), et.Color,
                et.PriceCents, et.PlatformFeeCents, et.TableTemplateId);

            foreach (var t in et.Tables)
            {
                await tableProc.CreateTableAsync(newEtId, copyId, t.Label, t.GridRow, t.GridCol, t.SortOrder);
            }
        }

        // Copy ticket types (Open events)
        var ticketTypes = await context.EventTicketTypes
            .Where(tt => tt.EventId == id && tt.IsActive)
            .ToListAsync();
        foreach (var tt in ticketTypes)
        {
            await ticketTypeProc.CreateAsync(copyId, tt.Label, tt.PriceCents,
                tt.PlatformFeeCents, tt.MaxQuantity, tt.SortOrder);
        }

        await adminLog.LogAsync("event.duplicated", "Event", copyId,
            $"Event duplicated from '{original.Title}'");

        var created = await context.EventViews.AsNoTracking().FirstAsync(e => e.Id == copyId);
        return Created("", MapToDto(created));
    }

    // ─── Ticket Type CRUD (Open events) ─────────────────────────

    [HttpGet("{id:guid}/ticket-types")]
    public async Task<IActionResult> GetTicketTypes(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var types = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == id)
            .OrderBy(tt => tt.SortOrder)
            .Select(tt => new EventTicketTypeDto(
                tt.Id, tt.Label, tt.PriceCents, tt.PlatformFeeCents,
                tt.MaxQuantity, tt.SortOrder, tt.IsActive,
                tt.SoldCount, tt.AvailableCount))
            .ToListAsync();

        return Ok(new EventTicketTypesResponse(id, types));
    }

    [HttpPost("{id:guid}/ticket-types")]
    public async Task<IActionResult> CreateTicketType(Guid id, [FromBody] CreateEventTicketTypeRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();
        if (ev.LayoutMode != "Open")
            return BadRequest(new ApiError(400, "Ticket types are only available for Open layout events", HttpContext.TraceIdentifier));

        var typeId = await ticketTypeProc.CreateAsync(
            id, request.Label, request.PriceCents,
            request.PlatformFeeCents, request.MaxQuantity, request.SortOrder);

        await adminLog.LogAsync("event.ticket_type.created", "EventTicketType", typeId,
            $"Ticket type '{request.Label}' created for event '{ev.Title}'");

        var created = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstAsync(tt => tt.Id == typeId);

        return Created("", new EventTicketTypeDto(
            created.Id, created.Label, created.PriceCents, created.PlatformFeeCents,
            created.MaxQuantity, created.SortOrder, created.IsActive,
            created.SoldCount, created.AvailableCount));
    }

    [HttpPut("{id:guid}/ticket-types/{typeId:guid}")]
    public async Task<IActionResult> UpdateTicketType(Guid id, Guid typeId, [FromBody] UpdateEventTicketTypeRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var existing = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstOrDefaultAsync(tt => tt.Id == typeId && tt.EventId == id);
        if (existing is null) return NotFound(new ApiError(404, "Ticket type not found", HttpContext.TraceIdentifier));

        if (request.PriceCents.HasValue && request.PriceCents != existing.PriceCents)
        {
            var hasPaidBookings = await context.BookingViews.AsNoTracking()
                .AnyAsync(b => b.EventTicketTypeId == typeId && (b.Status == "Paid" || b.Status == "CheckedIn"));
            if (hasPaidBookings)
                return BadRequest(new ApiError(400, "Cannot change price — paid bookings exist for this ticket type", HttpContext.TraceIdentifier));
        }

        await ticketTypeProc.UpdateAsync(typeId, request.Label, request.PriceCents,
            request.PlatformFeeCents, request.MaxQuantity, request.SortOrder, request.IsActive);

        var updated = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstAsync(tt => tt.Id == typeId);

        return Ok(new EventTicketTypeDto(
            updated.Id, updated.Label, updated.PriceCents, updated.PlatformFeeCents,
            updated.MaxQuantity, updated.SortOrder, updated.IsActive,
            updated.SoldCount, updated.AvailableCount));
    }

    [HttpDelete("{id:guid}/ticket-types/{typeId:guid}")]
    public async Task<IActionResult> DeleteTicketType(Guid id, Guid typeId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var existing = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstOrDefaultAsync(tt => tt.Id == typeId && tt.EventId == id);
        if (existing is null) return NotFound(new ApiError(404, "Ticket type not found", HttpContext.TraceIdentifier));

        var hasActiveBookings = await context.BookingViews.AsNoTracking()
            .AnyAsync(b => b.EventTicketTypeId == typeId && (b.Status == "Pending" || b.Status == "Paid" || b.Status == "CheckedIn"));
        if (hasActiveBookings)
            return BadRequest(new ApiError(400, "Cannot delete — active bookings exist for this ticket type", HttpContext.TraceIdentifier));

        await ticketTypeProc.DeleteAsync(typeId);
        return NoContent();
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private EventDto MapToDto(EventView e) => new(
        e.Id, e.Title, e.Slug, e.Description,
        e.Status, e.Category,
        e.StartDate, e.EndDate,
        e.ImagePath is not null ? fileStorage.GetPublicUrl(e.ImagePath) : null,
        e.IsFeatured,
        e.LayoutMode, e.MaxCapacity, e.PricePerPersonCents, e.PlatformFeePercent, e.PlatformFeeCents,
        e.GridRows, e.GridCols, e.PublishedAt,
        e.VenueId,
        new VenueDto(
            e.VenueId, e.VenueName, e.VenueAddress, e.VenueCity, e.VenueState,
            e.VenueZipCode, e.VenueDescription,
            e.VenueImagePath is not null ? fileStorage.GetPublicUrl(e.VenueImagePath) : null,
            e.VenuePhone, e.VenueEmail, e.VenueWebsite,
            e.VenueIsActive, e.VenueCreatedAt
        ),
        e.OrganizerId,
        $"{e.OrganizerFirstName} {e.OrganizerLastName}",
        e.CreatedAt,
        e.MaxCapacity ?? 0,
        e.TotalSold,
        e.AvailableTables,
        e.MinTablePriceCents,
        e.MinTicketTypePriceCents
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
