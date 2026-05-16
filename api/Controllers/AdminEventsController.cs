using System.Security.Claims;
using System.Text.RegularExpressions;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.DTOs.Events;
using Contracts.DTOs.Images;
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

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/events")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminEventsController(
    EventPlatformDbContext context,
    IEventProcedures eventProc,
    ITableProcedures tableProc,
    ILayoutProcedures layoutProc,
    IEventTicketTypeProcedures ticketTypeProc,
    IFileStorageService fileStorage,
    IAdminLogService adminLog,
    ISettingsService settingsService,
    IEventImageService eventImageService,
    IBusinessUserEventProcedures businessUserEventProc,
    IBusinessUserProcedures businessUserProc,
    IOrganizationProcedures organizationProc,
    ICacheService cache
) : ControllerBase
{

    protected EventPlatformDbContext Context => context;
    protected ISettingsService Settings => settingsService;
    protected IAdminLogService AdminLog => adminLog;
    protected ICacheService Cache => cache;

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

        if (!User.IsInRole(UserRole.Developer.ToString()))
        {
            var currentUserId = GetCurrentUserId();
            var callerOrg = await organizationProc.GetByBusinessUserAsync(currentUserId);
            List<Guid> coAdminIds;
            if (callerOrg is null)
            {
                coAdminIds = new List<Guid> { currentUserId };
            }
            else
            {
                var members = await organizationProc.GetMembersAsync(callerOrg.Id);
                coAdminIds = members.Select(m => m.BusinessUserId).ToList();
                if (!coAdminIds.Contains(currentUserId)) coAdminIds.Add(currentUserId);
            }
            query = query.Where(e => coAdminIds.Contains(e.BusinessUserId));
        }

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

        var gridEventIds = items.Where(e => e.LayoutMode == "Grid").Select(e => e.EventId).ToList();
        var tableStats = new Dictionary<Guid, (int total, int booked)>();
        if (gridEventIds.Any())
        {
            var stats = await context.EventTableStatsViews.AsNoTracking()
                .Where(s => gridEventIds.Contains(s.EventId))
                .ToListAsync();

            foreach (var stat in stats)
                tableStats[stat.EventId] = (stat.TotalTables, stat.BookedTables);
        }

        var dtos = items.Select(e =>
        {
            var dto = MapToDto(e);
            if (e.LayoutMode == "Grid" && tableStats.TryGetValue(e.EventId, out var ts))
            {
                return dto with { TotalTables = ts.total, BookedTables = ts.booked };
            }
            return dto;
        }).ToList();
        return Ok(new PagedResponse<EventDto>(dtos, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var dto = MapToDto(ev);

        if (ev.LayoutMode == "Open")
        {
            var ticketTypeViews = await context.EventTicketTypeSummaryViews.AsNoTracking()
                .Where(tt => tt.EventId == id && tt.IsActive)
                .OrderBy(tt => tt.SortOrder)
                .ToListAsync();

            dto = dto with
            {
                TicketTypes = ticketTypeViews.Select(tt => new EventTicketTypeDto(
                    tt.EventTicketTypeId, tt.Label, tt.PriceCents, tt.PlatformFeeCents,
                    tt.TotalPriceCents,
                    tt.MaxQuantity, tt.SortOrder, tt.IsActive,
                    tt.SoldCount, tt.AvailableCount,
                    IsSoldOut: tt.AvailableCount <= 0,
                    Description: tt.Description)).ToList()
            };
        }
        else if (ev.LayoutMode == "Grid")
        {
            var tableTypeViews = await context.EventTablesSummaryViews.AsNoTracking()
                .Where(t => t.EventId == id && t.IsActive)
                .OrderBy(t => t.Label)
                .ToListAsync();

            dto = dto with
            {
                TableTypes = tableTypeViews.Select(t => new EventTableTypeSummaryDto(
                    t.EventTableId, t.Label, t.Capacity, t.Shape, t.Color,
                    t.PriceCents, t.PlatformFeeCents,
                    t.PriceCents + (t.PlatformFeeCents ?? 0),
                    t.TotalTables, t.AvailableTables, t.BookedTables)).ToList()
            };
        }

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var venue = await context.VenueViews.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VenueId == request.VenueId);
        if (venue is null) return BadRequest(new ApiError(400, "Venue not found", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<EventCategory>(request.Category, true, out _))
            return BadRequest(new ApiError(400, "Invalid category", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<LayoutMode>(request.LayoutMode, true, out var layoutMode))
            return BadRequest(new ApiError(400, "Invalid layout mode", HttpContext.TraceIdentifier));

        var organizerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var slug = GenerateSlug(request.Title);

        var baseSlug = slug;
        var counter = 1;
        while (await context.EventViews.AsNoTracking().AnyAsync(e => e.Slug == slug))
            slug = $"{baseSlug}-{counter++}";

        var eventId = await eventProc.CreateEventAsync(
            request.Title, slug, request.Description, "Draft", request.Category,
            request.StartDate, request.EndDate, request.BannerImageUrl, request.IsFeatured,
            request.LayoutMode, request.MaxCapacity,
            layoutMode == LayoutMode.Open ? request.PricePerPersonCents : null,
            null, null, null, null,
            request.VenueId, organizerId, null);

        await adminLog.LogAsync("event.created", "Event", eventId, $"Event '{request.Title}' created");

        if (layoutMode == LayoutMode.Open && request.TicketTypes != null)
        {
            var defaultFeeStr = await settingsService.GetOrDefaultAsync("default_platform_fee_open_cents", "1000");
            var defaultFee = int.TryParse(defaultFeeStr, out var f) ? f : 1000;
            var sortOrder = 0;
            foreach (var tt in request.TicketTypes)
            {
                await ticketTypeProc.CreateAsync(eventId, tt.Name, tt.PriceCents, defaultFee, tt.Capacity, sortOrder++, tt.Description);
            }
        }

        var created = await context.EventViews.AsNoTracking().FirstAsync(e => e.EventId == eventId);
        await cache.InvalidateEventAsync(eventId);
        return CreatedAtAction(nameof(GetById), new { id = eventId }, MapToDto(created));
    }

    private Guid GetCurrentUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private async Task<bool> IsOwnerOrSameOrgOrDeveloperAsync(Guid organizerId)
    {
        if (User.IsInRole(UserRole.Developer.ToString())) return true;
        var currentUserId = GetCurrentUserId();
        if (organizerId == currentUserId) return true;

        var callerOrg = await organizationProc.GetByBusinessUserAsync(currentUserId);
        if (callerOrg is null) return false;
        var ownerOrg = await organizationProc.GetByBusinessUserAsync(organizerId);
        return ownerOrg is not null && ownerOrg.Id == callerOrg.Id;
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

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
                var hasPurchases = await context.PurchaseViews.AsNoTracking()
                    .AnyAsync(b => b.EventId == id && b.Status != "Cancelled" && b.Status != "Refunded");
                if (hasPurchases)
                    return BadRequest(new ApiError(400, "Cannot change layout mode — active purchases exist for this event", HttpContext.TraceIdentifier));
            }
        }

        await eventProc.UpdateEventAsync(
            id, request.Title, newSlug, request.Description, request.Category,
            request.StartDate, request.EndDate, request.BannerImageUrl, request.IsFeatured,
            request.LayoutMode, request.MaxCapacity, request.PricePerPersonCents,
            null, null, null, null, request.VenueId, null);

        if (newStatus is not null)
            await eventProc.ChangeEventStatusAsync(id, newStatus, null);

        if (request.TicketTypes != null && (request.LayoutMode == "Open" || (request.LayoutMode == null && ev.LayoutMode == "Open")))
        {
            var existingTiers = await context.EventTicketTypeSummaryViews
                .AsNoTracking()
                .Where(tt => tt.EventId == id && tt.IsActive)
                .ToListAsync();

            var matchedExistingIds = new HashSet<Guid>();
            var defaultFeeStr = await settingsService.GetOrDefaultAsync("default_platform_fee_open_cents", "1000");
            var defaultFee = int.TryParse(defaultFeeStr, out var df) ? df : 1000;
            var sortOrder = 0;

            foreach (var tt in request.TicketTypes)
            {
                EventTicketTypeSummaryView? match = null;
                if (tt.EventTicketTypeId.HasValue)
                {
                    match = existingTiers.FirstOrDefault(et => et.EventTicketTypeId == tt.EventTicketTypeId.Value);
                    if (match is null)
                        return BadRequest(new ApiError(400,
                            $"Ticket type {tt.EventTicketTypeId.Value} not found on this event",
                            HttpContext.TraceIdentifier));
                }
                else
                {
                    match = existingTiers.FirstOrDefault(et =>
                        !matchedExistingIds.Contains(et.EventTicketTypeId) &&
                        string.Equals(et.Label, tt.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (match is not null)
                {
                    matchedExistingIds.Add(match.EventTicketTypeId);

                    if (tt.PriceCents != match.PriceCents && match.SoldCount > 0)
                        return BadRequest(new ApiError(400,
                            $"Cannot change price for '{match.Label}' — {match.SoldCount} ticket(s) already sold",
                            HttpContext.TraceIdentifier));

                    if (tt.Capacity.HasValue && tt.Capacity.Value < match.SoldCount)
                        return BadRequest(new ApiError(400,
                            $"Cannot reduce '{match.Label}' capacity to {tt.Capacity.Value} — {match.SoldCount} already sold",
                            HttpContext.TraceIdentifier));

                    await ticketTypeProc.UpdateAsync(
                        match.EventTicketTypeId, tt.Name, tt.PriceCents,
                        match.PlatformFeeCents, tt.Capacity, sortOrder++, true, tt.Description);
                }
                else
                {
                    await ticketTypeProc.CreateAsync(id, tt.Name, tt.PriceCents, defaultFee, tt.Capacity, sortOrder++, tt.Description);
                }
            }

            var toRemove = existingTiers.Where(et => !matchedExistingIds.Contains(et.EventTicketTypeId)).ToList();
            foreach (var td in toRemove)
            {
                if (td.SoldCount > 0)
                    return BadRequest(new ApiError(400,
                        $"Cannot remove '{td.Label}' — {td.SoldCount} ticket(s) already sold",
                        HttpContext.TraceIdentifier));
                await ticketTypeProc.DeleteAsync(td.EventTicketTypeId);
            }
        }

        var updated = await context.EventViews.AsNoTracking().FirstAsync(e => e.EventId == id);
        await cache.InvalidateEventAsync(id);
        await cache.InvalidateTablesAsync(id);
        return Ok(MapToDto(updated));
    }

    [HttpGet("{id:guid}/layout-locked")]
    public async Task<IActionResult> IsLayoutModeLocked(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var hasPurchases = await context.PurchaseViews.AsNoTracking()
            .AnyAsync(b => b.EventId == id && b.Status != "Cancelled" && b.Status != "Refunded");

        return Ok(new { locked = hasPurchases });
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var path = await fileStorage.SaveAsync(file.OpenReadStream(), "events", file.FileName);
        await eventProc.UpdateEventAsync(id, null, null, null, null, null, null, path,
            null, null, null, null, null, null, null, null, null, null);

        return Ok(new { imageUrl = fileStorage.GetPublicUrl(path) });
    }

    [HttpPost("{eventId:guid}/images")]
    public async Task<IActionResult> AddEventImage(Guid eventId, IFormFile file, [FromForm] string? altText = null, [FromForm] string? caption = null)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var result = await eventImageService.AddAsync(eventId, file.OpenReadStream(), file.FileName, GetCurrentUserId(), altText, caption);
        await adminLog.LogAsync("event.image.add", "Event", eventId, $"Added image {result.ImageId}");
        return Ok(result);
    }

    [HttpGet("{eventId:guid}/images")]
    public async Task<IActionResult> ListEventImages(Guid eventId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var list = await eventImageService.ListAsync(eventId);
        return Ok(list);
    }

    [HttpPut("{eventId:guid}/images/order")]
    public async Task<IActionResult> ReorderEventImages(Guid eventId, [FromBody] ReorderEventImagesRequest request)
    {
        if (request.ImageIds is null || request.ImageIds.Count == 0)
            return BadRequest(new ApiError(400, "imageIds required", HttpContext.TraceIdentifier));
        if (request.ImageIds.Distinct().Count() != request.ImageIds.Count)
            return BadRequest(new ApiError(400, "imageIds must be unique", HttpContext.TraceIdentifier));

        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        await eventImageService.ReorderAsync(eventId, request.ImageIds);
        await adminLog.LogAsync("event.image.reorder", "Event", eventId, $"Reordered {request.ImageIds.Count} images");
        return NoContent();
    }

    [HttpPut("{eventId:guid}/images/{imageId:guid}/primary")]
    public async Task<IActionResult> SetEventImagePrimary(Guid eventId, Guid imageId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var ok = await eventImageService.SetPrimaryAsync(eventId, imageId);
        if (!ok) return NotFound(new ApiError(404, "Image not found on this event", HttpContext.TraceIdentifier));
        await adminLog.LogAsync("event.image.setPrimary", "Event", eventId, $"Set primary image {imageId}");
        return NoContent();
    }

    [HttpDelete("{eventId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteEventImage(Guid eventId, Guid imageId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var ok = await eventImageService.RemoveAsync(eventId, imageId);
        if (!ok) return NotFound(new ApiError(404, "Image not found on this event", HttpContext.TraceIdentifier));
        await adminLog.LogAsync("event.image.delete", "Event", eventId, $"Deleted image {imageId}");
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeEventStatusRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

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

        var updated = await context.EventViews.AsNoTracking().FirstAsync(e => e.EventId == id);
        await cache.InvalidateEventAsync(id);
        return Ok(MapToDto(updated));
    }

    [HttpGet("{id:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var stats = await eventProc.GetEventStatsAsync(id);
        if (stats is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        return Ok(new EventStatsDto(
            stats.TotalSold, stats.MaxCapacity, stats.FillRatePct, stats.GrossRevenueCents));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        if (ev.Status != EventStatus.Draft.ToString())
            return BadRequest(new ApiError(400, "Only draft events can be deleted", HttpContext.TraceIdentifier));

        var hasPurchases = await context.PurchaseViews.AsNoTracking().AnyAsync(b => b.EventId == id);
        if (hasPurchases)
            return BadRequest(new ApiError(400, "Cannot delete an event with purchases", HttpContext.TraceIdentifier));

        await eventProc.DeleteEventAsync(id);
        await cache.InvalidateEventAsync(id);
        await cache.InvalidateTablesAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, [FromBody] DuplicateEventRequest request)
    {
        var original = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (original is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(original.BusinessUserId)) return Forbid();

        var organizerId = GetCurrentUserId();
        var slug = GenerateSlug(original.Title + " copy");
        var baseSlug = slug;
        var counter = 1;
        while (await context.EventViews.AsNoTracking().AnyAsync(e => e.Slug == slug))
            slug = $"{baseSlug}-{counter++}";

        var copyId = await eventProc.CreateEventAsync(
            original.Title + " (Copy)", slug, original.Description, "Draft", original.Category,
            request.StartDate, request.EndDate, original.ImagePath, false,
            original.LayoutMode, original.MaxCapacity, original.PricePerPersonCents,
            null, null,
            original.GridRows, original.GridCols, original.VenueId, organizerId, null);

        var eventTables = await layoutProc.ListEventTablesForEventAsync(id);
        var allTables = await layoutProc.ListTablesForEventAsync(id);
        var tablesByEventTable = allTables.GroupBy(t => t.EventTableId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var et in eventTables)
        {
            var newEtId = await tableProc.CreateEventTableAsync(
                copyId, et.Label, et.Capacity, et.Shape.ToString(), et.Color,
                et.PriceCents, et.PlatformFeeCents, et.TableTemplateId);

            if (tablesByEventTable.TryGetValue(et.Id, out var tables))
            {
                foreach (var t in tables)
                {
                    await tableProc.CreateTableAsync(newEtId, copyId, t.Label, t.GridRow, t.GridCol, t.SortOrder);
                }
            }
        }

        var ticketTypes = await context.EventTicketTypeSummaryViews
            .AsNoTracking()
            .Where(tt => tt.EventId == id && tt.IsActive)
            .ToListAsync();
        foreach (var tt in ticketTypes)
        {
            await ticketTypeProc.CreateAsync(copyId, tt.Label, tt.PriceCents,
                tt.PlatformFeeCents, tt.MaxQuantity, tt.SortOrder, tt.Description);
        }

        await adminLog.LogAsync("event.duplicated", "Event", copyId,
            $"Event duplicated from '{original.Title}'");

        var created = await context.EventViews.AsNoTracking().FirstAsync(e => e.EventId == copyId);
        await cache.InvalidateEventAsync(copyId);
        return Created("", MapToDto(created));
    }

    [HttpGet("{id:guid}/ticket-types")]
    public async Task<IActionResult> GetTicketTypes(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var rawTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == id)
            .OrderBy(tt => tt.SortOrder)
            .ToListAsync();

        var types = rawTypes.Select(tt => new AdminEventTicketTypeDto(
            tt.EventTicketTypeId, tt.Label, tt.PriceCents, tt.PlatformFeeCents,
            tt.TotalPriceCents,
            tt.MaxQuantity, tt.SortOrder, tt.IsActive,
            tt.SoldCount, tt.AvailableCount,
            IsSoldOut: tt.AvailableCount <= 0,
            Description: tt.Description)).ToList();

        return Ok(new AdminEventTicketTypesResponse(id, types));
    }

    [HttpPost("{id:guid}/ticket-types")]
    public async Task<IActionResult> CreateTicketType(Guid id, [FromBody] CreateEventTicketTypeRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();
        if (ev.LayoutMode != "Open")
            return BadRequest(new ApiError(400, "Ticket types are only available for Open layout events", HttpContext.TraceIdentifier));

        var defaultFeeCents = int.Parse(await settingsService.GetOrDefaultAsync("default_platform_fee_open_cents", "1000") ?? "1000");
        var resolvedFee = request.PlatformFeeCents ?? defaultFeeCents;

        var typeId = await ticketTypeProc.CreateAsync(
            id, request.Label, request.PriceCents,
            resolvedFee, request.MaxQuantity, request.SortOrder, request.Description);

        await adminLog.LogAsync("event.ticket_type.created", "EventTicketType", typeId,
            $"Ticket type '{request.Label}' created for event '{ev.Title}'");

        var created = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstAsync(tt => tt.EventTicketTypeId == typeId);

        return Created("", new AdminEventTicketTypeDto(
            created.EventTicketTypeId, created.Label, created.PriceCents, created.PlatformFeeCents,
            created.TotalPriceCents,
            created.MaxQuantity, created.SortOrder, created.IsActive,
            created.SoldCount, created.AvailableCount,
            IsSoldOut: created.AvailableCount <= 0,
            Description: created.Description));
    }

    [HttpPut("{id:guid}/ticket-types/{typeId:guid}")]
    public async Task<IActionResult> UpdateTicketType(Guid id, Guid typeId, [FromBody] UpdateEventTicketTypeRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var existing = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstOrDefaultAsync(tt => tt.EventTicketTypeId == typeId && tt.EventId == id);
        if (existing is null) return NotFound(new ApiError(404, "Ticket type not found", HttpContext.TraceIdentifier));

        var isPriceChange = (request.PriceCents.HasValue && request.PriceCents != existing.PriceCents)
            || (request.PlatformFeeCents.HasValue && request.PlatformFeeCents != existing.PlatformFeeCents);
        if (isPriceChange)
        {
            var hasActivePurchases = await context.PurchaseViews.AsNoTracking()
                .AnyAsync(b => b.EventTicketTypeId == typeId
                    && b.Status != "Cancelled" && b.Status != "Expired" && b.Status != "Refunded");
            if (hasActivePurchases)
                return BadRequest(new ApiError(400, "Cannot change pricing — tickets have been sold or locked for this ticket type", HttpContext.TraceIdentifier));
        }

        if (request.MaxQuantity.HasValue && request.MaxQuantity.Value < existing.SoldCount)
            return BadRequest(new ApiError(400,
                $"Cannot reduce capacity to {request.MaxQuantity.Value} — {existing.SoldCount} ticket(s) already sold",
                HttpContext.TraceIdentifier));

        await ticketTypeProc.UpdateAsync(typeId, request.Label, request.PriceCents,
            request.PlatformFeeCents, request.MaxQuantity, request.SortOrder, request.IsActive, request.Description);

        var updated = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstAsync(tt => tt.EventTicketTypeId == typeId);

        return Ok(new AdminEventTicketTypeDto(
            updated.EventTicketTypeId, updated.Label, updated.PriceCents, updated.PlatformFeeCents,
            updated.TotalPriceCents,
            updated.MaxQuantity, updated.SortOrder, updated.IsActive,
            updated.SoldCount, updated.AvailableCount,
            IsSoldOut: updated.AvailableCount <= 0,
            Description: updated.Description));
    }

    [HttpDelete("{id:guid}/ticket-types/{typeId:guid}")]
    public async Task<IActionResult> DeleteTicketType(Guid id, Guid typeId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var existing = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .FirstOrDefaultAsync(tt => tt.EventTicketTypeId == typeId && tt.EventId == id);
        if (existing is null) return NotFound(new ApiError(404, "Ticket type not found", HttpContext.TraceIdentifier));

        var hasActivePurchases = await context.PurchaseViews.AsNoTracking()
            .AnyAsync(b => b.EventTicketTypeId == typeId && (b.Status == "Pending" || b.Status == "Paid" || b.Status == "CheckedIn"));
        if (hasActivePurchases)
            return BadRequest(new ApiError(400, "Cannot delete — active purchases exist for this ticket type", HttpContext.TraceIdentifier));

        await ticketTypeProc.DeleteAsync(typeId);
        return NoContent();
    }

    [HttpGet("{eventId:guid}/staff")]
    public async Task<IActionResult> ListEventStaff(Guid eventId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var rows = await businessUserEventProc.ListStaffForEventAsync(eventId);
        var dtos = rows.Select(r => new EventStaffDto(
            r.BusinessUserEventId, r.BusinessUserId, r.FirstName, r.LastName, r.Email, r.CreatedAt)).ToList();
        return Ok(new { items = dtos });
    }

    [HttpPost("{eventId:guid}/staff")]
    public async Task<IActionResult> AssignEventStaff(Guid eventId, [FromBody] AssignStaffRequest request)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        var target = await businessUserProc.GetByIdAsync(request.BusinessUserId);
        if (target is null)
            return NotFound(new ApiError(404, "Staff user not found", HttpContext.TraceIdentifier));
        if (!target.IsActive)
            return BadRequest(new ApiError(400, "Cannot assign a disabled account", HttpContext.TraceIdentifier));
        if (target.Role != AdminRole.Staff)
            return BadRequest(new ApiError(400, "Only Staff users can be assigned to events", HttpContext.TraceIdentifier));

        var assignmentId = await businessUserEventProc.AssignAsync(request.BusinessUserId, eventId, GetCurrentUserId());

        await adminLog.LogAsync("staff.assigned_to_event", "BusinessUserEvent", eventId,
            $"Assigned {target.FirstName} {target.LastName} to event '{ev.Title}'");

        return Ok(new { businessUserEventId = assignmentId, message = "Staff assigned" });
    }

    [HttpDelete("{eventId:guid}/staff/{businessUserId:guid}")]
    public async Task<IActionResult> UnassignEventStaff(Guid eventId, Guid businessUserId)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!await IsOwnerOrSameOrgOrDeveloperAsync(ev.BusinessUserId)) return Forbid();

        await businessUserEventProc.UnassignAsync(businessUserId, eventId);

        await adminLog.LogAsync("staff.unassigned_from_event", "BusinessUserEvent", eventId,
            $"Unassigned staff {businessUserId} from event '{ev.Title}'");

        return NoContent();
    }

    private EventDto MapToDto(EventView e)
    {
        var displayFrom = MinNonNull(e.DisplayMinTablePriceCents, e.DisplayMinTicketTypePriceCents);
        var displayFromFormatted = displayFrom.HasValue ? $"${displayFrom.Value / 100.0:F2}" : null;
        var isSoldOut = e.LayoutMode == "Grid"
            ? e.AvailableTables <= 0
            : (e.TotalCapacity > 0 && e.TotalSold >= e.TotalCapacity);
        var availableCount = e.LayoutMode == "Grid"
            ? e.AvailableTables
            : Math.Max(0, e.TotalCapacity - e.TotalSold);

        return new EventDto(
            e.EventId, e.Title, e.Slug, e.Description,
            e.Status, e.Category,
            e.StartDate, e.EndDate,
            e.ImagePath is not null ? fileStorage.GetPublicUrl(e.ImagePath) : null,
            e.IsFeatured,
            e.LayoutMode, e.MaxCapacity,
            e.GridRows, e.GridCols, e.PublishedAt,
            e.VenueId,
            e.VenueName,
            null,
            e.BusinessUserId,
            $"{e.OrganizerFirstName} {e.OrganizerLastName}",
            e.CreatedAt,
            e.MaxCapacity ?? 0,
            e.TotalSold,
            e.AvailableTables,
            displayFrom,
            displayFromFormatted,
            isSoldOut,
            availableCount,
            PricePerPersonCents: e.PricePerPersonCents
        );
    }

    private static int? MinNonNull(params int?[] values)
    {
        var nonNull = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return nonNull.Length == 0 ? null : nonNull.Min();
    }

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
