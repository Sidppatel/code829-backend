using System.Security.Claims;
using System.Text.Json;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Events;
using Contracts.DTOs.Venues;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Controllers;

[ApiController]
[Route("events")]
public class EventsController(
    EventPlatformDbContext context,
    IFileStorageService fileStorage,
    ISettingsService settings,
    IConnectionMultiplexer redis
) : ControllerBase
{
    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromSeconds(30);

    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? city = null,
        [FromQuery] string? dateFilter = null,
        [FromQuery] int? minPrice = null,
        [FromQuery] int? maxPrice = null,
        [FromQuery] Guid? venueId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        var maxPageSize = int.Parse(await settings.GetOrDefaultAsync("search_results_per_page", "20") ?? "20");
        if (pageSize < 1 || pageSize > maxPageSize) pageSize = maxPageSize;

        var cacheKey = $"events:list:{search}:{category}:{city}:{dateFilter}:{minPrice}:{maxPrice}:{venueId}:{page}:{pageSize}";
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return Content(cached.ToString(), "application/json");

        var query = context.EventSummaryViews
            .AsNoTracking()
            .Where(e => e.Status == "Published")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();

            if (trimmedSearch.Length < 3)
            {
                query = query.Where(e =>
                    EF.Functions.ILike(e.Title, $"%{trimmedSearch}%") ||
                    EF.Functions.ILike(e.VenueName, $"%{trimmedSearch}%"));
            }
            else
            {
                var ftsIds = await context.Events
                    .Where(e => e.Status == EventStatus.Published && e.SearchVector!.Matches(EF.Functions.PlainToTsQuery(trimmedSearch)))
                    .Select(e => e.Id)
                    .ToListAsync();

                var trigramIds = await context.Events
                    .Where(e => e.Status == EventStatus.Published)
                    .Where(e => EF.Functions.TrigramsSimilarity(e.Title, trimmedSearch) > 0.1)
                    .Select(e => e.Id)
                    .ToListAsync();

                var matchIds = ftsIds.Union(trigramIds).Distinct().ToList();

                if (matchIds.Count == 0)
                {
                    query = query.Where(e =>
                        EF.Functions.ILike(e.Title, $"%{trimmedSearch}%") ||
                        EF.Functions.ILike(e.VenueName, $"%{trimmedSearch}%"));
                }
                else
                {
                    query = query.Where(e => matchIds.Contains(e.Id));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(e => EF.Functions.ILike(e.VenueCity, city));

        if (venueId.HasValue)
            query = query.Where(e => e.VenueId == venueId.Value);

        if (minPrice.HasValue)
            query = query.Where(e => e.PricePerPersonCents >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(e => e.PricePerPersonCents <= maxPrice.Value);

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dateFilter))
        {
            query = dateFilter.ToLower() switch
            {
                "today" => query.Where(e => e.StartDate.Date == now.Date),
                "this-week" => query.Where(e => e.StartDate >= now && e.StartDate <= now.AddDays(7)),
                "this-month" => query.Where(e => e.StartDate >= now && e.StartDate <= now.AddDays(30)),
                _ => query.Where(e => e.StartDate >= now)
            };
        }
        else
        {
            query = query.Where(e => e.EndDate >= now);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(e => e.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var defaultOpenFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_open_cents", "1000") ?? "1000");

        var dtos = items.Select(e =>
        {
            var displayPricePerPerson = e.PricePerPersonCents.HasValue ? e.PricePerPersonCents.Value + defaultOpenFee : (int?)null;
            var displayFrom = MinNonNull(e.DisplayMinTablePriceCents, displayPricePerPerson, e.DisplayMinTicketTypePriceCents);
            var displayFromFormatted = displayFrom.HasValue ? $"${displayFrom.Value / 100.0:F2}" : null;
            var isSoldOut = e.LayoutMode == "Grid"
                ? e.AvailableTables <= 0
                : (e.TotalCapacity > 0 && e.TotalSold >= e.TotalCapacity);
            var availableCount = e.LayoutMode == "Grid"
                ? e.AvailableTables
                : Math.Max(0, e.TotalCapacity - e.TotalSold);

            return new EventSummaryDto(
                e.Id, e.Title, e.Slug, e.Status, e.Category,
                e.StartDate, e.EndDate,
                e.ImagePath != null
                    ? fileStorage.GetPublicUrl(e.ImagePath)
                    : e.PrimaryImageKey != null ? fileStorage.GetPublicUrl($"{e.PrimaryImageKey}_card.webp") : null,
                e.IsFeatured,
                e.LayoutMode,
                e.VenueName, e.VenueCity, e.VenueState,
                e.TotalCapacity,
                e.TotalSold,
                e.AvailableTables,
                displayFrom,
                displayFromFormatted,
                isSoldOut,
                availableCount);
        }).ToList();

        var result = new PagedResponse<EventSummaryDto>(dtos, totalCount, page, pageSize);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await db.StringSetAsync(cacheKey, json, ListCacheTtl);

        return Ok(result);
    }

    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets()
    {
        var now = DateTime.UtcNow;
        var published = context.EventSummaryViews
            .AsNoTracking()
            .Where(e => e.Status == "Published" && e.EndDate >= now);

        var categories = await published
            .Select(e => e.Category)
            .Distinct().ToListAsync();

        var cities = await published
            .Select(e => e.VenueCity)
            .Distinct().OrderBy(c => c).ToListAsync();

        var venues = await published
            .Select(e => new { e.VenueId, Name = e.VenueName })
            .Distinct().ToListAsync();

        var priceRange = await published
            .Where(e => e.PricePerPersonCents.HasValue)
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(e => e.PricePerPersonCents), Max = g.Max(e => e.PricePerPersonCents) })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            categories,
            cities,
            venues = venues.Select(v => new { v.VenueId, v.Name }),
            priceRange = new { min = priceRange?.Min ?? 0, max = priceRange?.Max ?? 0 }
        });
    }

    [HttpGet("schema-list")]
    public async Task<IActionResult> GetItemListSchema()
    {
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var now = DateTime.UtcNow;

        var events = await context.EventSummaryViews
            .AsNoTracking()
            .Where(e => e.Status == "Published" && e.EndDate >= now)
            .OrderBy(e => e.StartDate)
            .Take(50)
            .Select(e => new { e.Title, e.Slug })
            .ToListAsync();

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ItemList",
            ["name"] = "Upcoming Events",
            ["url"] = $"{frontendUrl}/events",
            ["numberOfItems"] = events.Count,
            ["itemListElement"] = events.Select((e, i) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["url"] = $"{frontendUrl}/events/{e.Slug}",
                ["name"] = e.Title
            }).ToList()
        };

        return Ok(schema);
    }

    [HttpGet("{id:guid}/seo")]
    public async Task<IActionResult> GetSeoMeta(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound();

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var dateStr = ev.StartDate.ToString("MMM d, yyyy");
        var description = ev.Description?.Length > 160 ? ev.Description[..157] + "..." : ev.Description ?? "";
        var canonicalUrl = $"{frontendUrl}/events/{ev.Slug}";

        return Ok(new
        {
            title = $"{ev.Title} — {dateStr} — {ev.VenueCity} | Code829",
            description,
            canonicalUrl,
            og = new
            {
                type = "website",
                title = ev.Title,
                description,
                url = canonicalUrl,
                site_name = appName
            },
            twitter = new
            {
                card = "summary_large_image",
                title = ev.Title,
                description
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == "Published");

        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var imageUrl = ev.ImagePath is not null
            ? fileStorage.GetPublicUrl(ev.ImagePath)
            : await ResolveEventImageUrlAsync(ev.Id);
        var defaultOpenFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_open_cents", "1000") ?? "1000");

        return Ok(MapEventDto(ev, imageUrl, defaultOpenFee));
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Slug == slug && e.Status == "Published");

        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var imageUrl = ev.ImagePath is not null
            ? fileStorage.GetPublicUrl(ev.ImagePath)
            : await ResolveEventImageUrlAsync(ev.Id);
        var defaultOpenFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_open_cents", "1000") ?? "1000");

        return Ok(MapEventDto(ev, imageUrl, defaultOpenFee));
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<IActionResult> GetSchemaOrg(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == "Published");

        if (ev is null) return NotFound();

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";

        var priceCents = ev.PricePerPersonCents ?? 0;

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Event",
            ["name"] = ev.Title,
            ["description"] = ev.Description,
            ["startDate"] = ev.StartDate.ToString("o"),
            ["endDate"] = ev.EndDate.ToString("o"),
            ["eventStatus"] = "https://schema.org/EventScheduled",
            ["eventAttendanceMode"] = "https://schema.org/OfflineEventAttendanceMode",
            ["url"] = $"{frontendUrl}/events/{ev.Slug}",
            ["location"] = new Dictionary<string, object?>
            {
                ["@type"] = "Place",
                ["name"] = ev.VenueName,
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = ev.VenueAddress,
                    ["addressLocality"] = ev.VenueCity,
                    ["addressRegion"] = ev.VenueState,
                    ["postalCode"] = ev.VenueZipCode,
                    ["addressCountry"] = "US"
                }
            },
            ["organizer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = appName
            },
            ["offers"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["@type"] = "Offer",
                    ["price"] = (priceCents / 100.0).ToString("F2"),
                    ["priceCurrency"] = "USD",
                    ["url"] = $"{frontendUrl}/events/{ev.Slug}"
                }
            }
        };

        return Ok(schema);
    }

    [HttpGet("{id:guid}/tables")]
    public async Task<IActionResult> GetTables(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        Guid? userId = null;
        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userClaim is not null && Guid.TryParse(userClaim.Value, out var uid)) userId = uid;

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == id && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        var eventTableTypes = await context.EventTablesSummaryViews.AsNoTracking()
            .Where(et => et.EventId == id)
            .Select(et => new EventTableTypeInfo(
                et.Id, et.Label, et.Capacity, et.Shape, et.Color, et.PriceCents,
                et.PriceCents + (et.PlatformFeeCents ?? 0)))
            .ToListAsync();

        var dtos = tables.Select(t =>
        {
            string status;
            DateTime? holdExpiresAt = null;
            var isLockedByYou = false;

            switch (t.Status)
            {
                case "Booked":
                    status = "Booked";
                    break;
                case "Locked":
                    isLockedByYou = userId.HasValue && t.LockedByUserId == userId.Value;
                    status = isLockedByYou ? "HeldByYou" : "Held";
                    holdExpiresAt = isLockedByYou ? t.LockExpiresAt : null;
                    break;
                default:
                    status = "Available";
                    break;
            }

            return new EventTableDto(t.Id, t.Label, t.Capacity,
                t.Shape, t.Color, t.PriceCents, t.TotalPriceCents,
                t.GridRow, t.GridCol, t.SortOrder, status, holdExpiresAt, isLockedByYou,
                t.EventTableId, t.EventTableLabel);
        }).ToList();

        return Ok(new EventTablesResponse(id, ev.GridRows, ev.GridCols, eventTableTypes, dtos));
    }

    [HttpGet("{id:guid}/ticket-types")]
    public async Task<IActionResult> GetTicketTypes(Guid id)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var rawTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == id && tt.IsActive)
            .OrderBy(tt => tt.SortOrder)
            .ToListAsync();

        var types = rawTypes.Select(tt => new EventTicketTypeDto(
            tt.Id, tt.Label, tt.PriceCents, null,
            tt.TotalPriceCents,
            tt.MaxQuantity, tt.SortOrder, tt.IsActive,
            tt.SoldCount, tt.AvailableCount)).ToList();

        return Ok(new EventTicketTypesResponse(id, types));
    }

    private EventDto MapEventDto(Db.Entities.Views.EventView ev, string? imageUrl, int defaultOpenFee)
    {
        var displayPricePerPerson = ev.PricePerPersonCents.HasValue ? ev.PricePerPersonCents.Value + defaultOpenFee : (int?)null;
        var displayFrom = MinNonNull(ev.DisplayMinTablePriceCents, displayPricePerPerson, ev.DisplayMinTicketTypePriceCents);
        var displayFromFormatted = displayFrom.HasValue ? $"${displayFrom.Value / 100.0:F2}" : null;
        var isSoldOut = ev.LayoutMode == "Grid"
            ? ev.AvailableTables <= 0
            : (ev.TotalCapacity > 0 && ev.TotalSold >= ev.TotalCapacity);
        var availableCount = ev.LayoutMode == "Grid"
            ? ev.AvailableTables
            : Math.Max(0, ev.TotalCapacity - ev.TotalSold);

        return new EventDto(
            ev.Id, ev.Title, ev.Slug, ev.Description,
            ev.Status, ev.Category,
            ev.StartDate, ev.EndDate,
            imageUrl,
            ev.IsFeatured,
            ev.LayoutMode,
            ev.MaxCapacity ?? ev.TotalCapacity,
            ev.GridRows, ev.GridCols, ev.PublishedAt,
            ev.VenueId,
            ev.VenueName,
            new VenueDto(
                ev.VenueId, ev.VenueName, ev.VenueAddress, ev.VenueCity, ev.VenueState,
                ev.VenueZipCode, ev.VenueDescription,
                ev.VenueImagePath is not null ? fileStorage.GetPublicUrl(ev.VenueImagePath) : null,
                ev.VenuePhone, ev.VenueEmail, ev.VenueWebsite,
                ev.VenueIsActive, ev.VenueCreatedAt
            ),
            ev.OrganizerId,
            $"{ev.OrganizerFirstName} {ev.OrganizerLastName}",
            ev.CreatedAt,
            ev.TotalCapacity,
            ev.TotalSold,
            ev.AvailableTables,
            displayFrom,
            displayFromFormatted,
            isSoldOut,
            availableCount);
    }

    private static int? MinNonNull(params int?[] values)
    {
        var nonNull = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return nonNull.Length == 0 ? null : nonNull.Min();
    }

    private async Task<string?> ResolveEventImageUrlAsync(Guid eventId)
    {
        var primary = await context.Images
            .Where(img => img.EntityType == "event" && img.EntityId == eventId && img.IsPrimary)
            .Select(img => img.StorageKey)
            .FirstOrDefaultAsync();

        return primary is not null ? fileStorage.GetPublicUrl($"{primary}_card.webp") : null;
    }
}
