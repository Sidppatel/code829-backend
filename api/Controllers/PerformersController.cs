using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Events;
using Db;
using Db.Entities.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/performers")]
public class PerformersController(
    EventPlatformDbContext context,
    IPerformerService performerService,
    IFileStorageService fileStorage
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await performerService.SearchAsync(q, page, pageSize, includePrivateMeta: false, ct);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct = default)
    {
        var dto = await performerService.GetBySlugAsync(slug, includePrivateMeta: false, ct);
        if (dto is null) return NotFound(new ApiError(404, "Performer not found", HttpContext.TraceIdentifier));
        return Ok(dto);
    }

    [HttpGet("/v{version:apiVersion}/events/by-slug/{eventSlug}/performers")]
    public async Task<IActionResult> GetEventLineup(string eventSlug, CancellationToken ct = default)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == eventSlug && e.Status == "Published", ct);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        var lineup = await performerService.ParseEventViewPerformersAsync(ev.Performers, includePrivateMeta: false);
        return Ok(lineup);
    }

    [HttpGet("{slug}/events")]
    public async Task<IActionResult> GetEvents(string slug, [FromQuery] string status = "upcoming", [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var performer = await context.PerformerViews.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (performer is null) return NotFound(new ApiError(404, "Performer not found", HttpContext.TraceIdentifier));

        var query = context.EventViews.AsNoTracking()
            .Where(e => e.Status == "Published")
            .Where(e => EF.Functions.JsonContains(e.Performers, $"[{{\"performerId\":\"{performer.PerformerId}\"}}]"));

        if (status == "upcoming")
            query = query.Where(e => e.EndDate >= DateTime.UtcNow);
        else if (status == "past")
            query = query.Where(e => e.EndDate < DateTime.UtcNow);

        query = status == "past"
            ? query.OrderByDescending(e => e.StartDate)
            : query.OrderBy(e => e.StartDate);

        var total = await query.CountAsync(ct);
        var pagedEventIds = await query.Select(e => e.EventId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var summaries = await context.EventSummaryViews.AsNoTracking()
            .Where(e => pagedEventIds.Contains(e.EventId))
            .ToListAsync(ct);

        var items = pagedEventIds.Select(id => summaries.First(s => s.EventId == id)).ToList();

        var dtos = items.Select(MapEventSummary).ToList();
        return Ok(new PagedResponse<EventSummaryDto>(dtos, total, page, pageSize));
    }

    private EventSummaryDto MapEventSummary(EventSummaryView v)
    {
        var imageUrl = v.ImagePath != null 
            ? fileStorage.GetPublicUrl(v.ImagePath) 
            : v.PrimaryImageKey != null 
                ? fileStorage.GetPublicUrl($"{v.PrimaryImageKey}_card.webp") 
                : null;
        var displayFrom = v.DisplayMinTicketTypePriceCents ?? v.DisplayMinTablePriceCents ?? v.PricePerPersonCents;
        var displayFromFormatted = displayFrom.HasValue ? $"${displayFrom.Value / 100.0:F2}" : null;
        var isSoldOut = v.LayoutMode == "Grid"
            ? v.AvailableTables <= 0
            : (v.TotalCapacity > 0 && v.TotalSold >= v.TotalCapacity);
        var availableCount = v.LayoutMode == "Grid"
            ? v.AvailableTables
            : Math.Max(0, v.TotalCapacity - v.TotalSold);

        return new EventSummaryDto(
            v.EventId,
            v.Title,
            v.Slug,
            v.Status,
            v.Category,
            v.StartDate,
            v.EndDate,
            imageUrl,
            v.IsFeatured,
            v.LayoutMode,
            v.VenueName,
            v.VenueCity,
            v.VenueState,
            v.TotalCapacity,
            v.TotalSold,
            v.AvailableTables,
            displayFrom,
            displayFromFormatted,
            isSoldOut,
            availableCount
        );
    }
}
