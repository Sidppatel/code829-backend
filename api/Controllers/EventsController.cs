using System.Text.Json;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Events;
using Contracts.DTOs.Venues;
using Contracts.Enums;
using Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
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

    /// <summary>
    /// Public event listing with full-text search (tsvector + trigram fallback for typo tolerance),
    /// faceted filters (category, city, date, price range, venue), and Redis caching.
    /// </summary>
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

        // Redis cache for non-search requests (browsing)
        var cacheKey = $"events:list:{search}:{category}:{city}:{dateFilter}:{minPrice}:{maxPrice}:{venueId}:{page}:{pageSize}";
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            return Content(cached.ToString(), "application/json");
        }

        var query = context.Events
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .Where(e => e.Status == EventStatus.Published)
            .AsQueryable();

        // Full-text search: tsvector match + trigram similarity for typo tolerance
        // Uses separate conditions to avoid client evaluation issues
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Get IDs matching via tsvector full-text search
            var ftsIds = await context.Events
                .Where(e => e.Status == EventStatus.Published && e.SearchVector!.Matches(EF.Functions.PlainToTsQuery("english", search)))
                .Select(e => e.Id)
                .ToListAsync();

            // Get IDs matching via trigram similarity (typo tolerance)
            var trigramIds = await context.Events
                .Where(e => e.Status == EventStatus.Published)
                .Where(e => EF.Functions.TrigramsSimilarity(e.Title, search) > 0.1)
                .Select(e => e.Id)
                .ToListAsync();

            var matchIds = ftsIds.Union(trigramIds).Distinct().ToList();
            query = query.Where(e => matchIds.Contains(e.Id));
        }

        // Category filter
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<EventCategory>(category, true, out var cat))
            query = query.Where(e => e.Category == cat);

        // City filter
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(e => e.Venue.City.ToLower() == city.ToLower());

        // Venue filter
        if (venueId.HasValue)
            query = query.Where(e => e.VenueId == venueId.Value);

        // Price range filter (in cents, based on min ticket price)
        if (minPrice.HasValue)
            query = query.Where(e => e.TicketTypes.Any(t => t.PriceCents >= minPrice.Value));
        if (maxPrice.HasValue)
            query = query.Where(e => e.TicketTypes.Any(t => t.PriceCents <= maxPrice.Value));

        // Date filter
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

        // Order by date (relevance is already handled by the ID filtering above)
        var items = await query
            .OrderBy(e => e.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventSummaryDto(
                e.Id, e.Title, e.Slug, e.Status.ToString(), e.Category.ToString(),
                e.StartDate, e.EndDate,
                e.ImagePath != null ? fileStorage.GetPublicUrl(e.ImagePath) : null,
                e.IsFeatured,
                e.Venue.Name, e.Venue.City, e.Venue.State,
                e.TicketTypes.Any() ? e.TicketTypes.Min(t => t.PriceCents) : null,
                e.TicketTypes.Any() ? e.TicketTypes.Max(t => t.PriceCents) : null,
                e.TicketTypes.Sum(t => t.QuantityTotal),
                e.TicketTypes.Sum(t => t.QuantitySold)
            ))
            .ToListAsync();

        var result = new PagedResponse<EventSummaryDto>(items, totalCount, page, pageSize);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await db.StringSetAsync(cacheKey, json, ListCacheTtl);

        return Ok(result);
    }

    /// <summary>
    /// Get available filter facets (categories, cities, price range).
    /// </summary>
    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets()
    {
        var now = DateTime.UtcNow;
        var published = context.Events
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .Where(e => e.Status == EventStatus.Published && e.EndDate >= now);

        var categories = await published
            .Select(e => e.Category.ToString())
            .Distinct().ToListAsync();

        var cities = await published
            .Select(e => e.Venue.City)
            .Distinct().OrderBy(c => c).ToListAsync();

        var venues = await published
            .Select(e => new { e.VenueId, e.Venue.Name })
            .Distinct().ToListAsync();

        var priceRange = await published
            .SelectMany(e => e.TicketTypes)
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(t => t.PriceCents), Max = g.Max(t => t.PriceCents) })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            categories,
            cities,
            venues = venues.Select(v => new { v.VenueId, v.Name }),
            priceRange = new { min = priceRange?.Min ?? 0, max = priceRange?.Max ?? 0 }
        });
    }

    /// <summary>
    /// ItemList schema.org for events listing page (for SEO).
    /// </summary>
    [HttpGet("schema-list")]
    public async Task<IActionResult> GetItemListSchema()
    {
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var now = DateTime.UtcNow;

        var events = await context.Events
            .Where(e => e.Status == EventStatus.Published && e.EndDate >= now)
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

    /// <summary>
    /// SEO metadata for a single event — OG tags, Twitter Card, canonical URL, Schema.org.
    /// </summary>
    [HttpGet("{id:guid}/seo")]
    public async Task<IActionResult> GetSeoMeta(Guid id)
    {
        var ev = await context.Events
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound();

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var brandName = await settings.GetOrDefaultAsync("brand_name", "Code829");
        var dateStr = ev.StartDate.ToString("MMM d, yyyy");
        var description = ev.Description?.Length > 160 ? ev.Description[..157] + "..." : ev.Description ?? "";
        var canonicalUrl = $"{frontendUrl}/events/{ev.Slug}";

        return Ok(new
        {
            title = $"{ev.Title} — {dateStr} — {ev.Venue.City} | {brandName}",
            description,
            canonicalUrl,
            og = new
            {
                type = "website",
                title = ev.Title,
                description,
                url = canonicalUrl,
                site_name = brandName
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
        var ev = await context.Events
            .Include(e => e.Venue)
            .Include(e => e.Organizer)
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null) return NotFound(new { message = "Event not found" });

        var dto = new EventDto(
            ev.Id, ev.Title, ev.Slug, ev.Description,
            ev.Status.ToString(), ev.Category.ToString(),
            ev.StartDate, ev.EndDate,
            ev.ImagePath is not null ? fileStorage.GetPublicUrl(ev.ImagePath) : null,
            ev.IsFeatured,
            ev.LayoutMode.ToString(), ev.MaxCapacity, ev.PlatformFeePercent, ev.PublishedAt,
            ev.VenueId,
            new VenueDto(
                ev.Venue.Id, ev.Venue.Name, ev.Venue.Address, ev.Venue.City, ev.Venue.State,
                ev.Venue.ZipCode, ev.Venue.Capacity, ev.Venue.Description,
                ev.Venue.ImagePath is not null ? fileStorage.GetPublicUrl(ev.Venue.ImagePath) : null,
                ev.Venue.Phone, ev.Venue.Website, ev.Venue.Latitude, ev.Venue.Longitude,
                ev.Venue.IsActive, ev.Venue.CreatedAt
            ),
            ev.OrganizerId,
            ev.Organizer?.Name,
            ev.TicketTypes.OrderBy(t => t.SortOrder).Select(t => new TicketTypeDto(
                t.Id, t.Name, t.Description, t.PriceCents,
                t.QuantityTotal, t.QuantitySold, t.QuantityTotal - t.QuantitySold, t.SortOrder
            )).ToList(),
            ev.CreatedAt
        );

        return Ok(dto);
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var ev = await context.Events
            .Include(e => e.Venue)
            .Include(e => e.Organizer)
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Slug == slug);

        if (ev is null) return NotFound(new { message = "Event not found" });

        return Ok(new EventDto(
            ev.Id, ev.Title, ev.Slug, ev.Description,
            ev.Status.ToString(), ev.Category.ToString(),
            ev.StartDate, ev.EndDate,
            ev.ImagePath is not null ? fileStorage.GetPublicUrl(ev.ImagePath) : null,
            ev.IsFeatured,
            ev.LayoutMode.ToString(), ev.MaxCapacity, ev.PlatformFeePercent, ev.PublishedAt,
            ev.VenueId,
            new VenueDto(
                ev.Venue.Id, ev.Venue.Name, ev.Venue.Address, ev.Venue.City, ev.Venue.State,
                ev.Venue.ZipCode, ev.Venue.Capacity, ev.Venue.Description,
                ev.Venue.ImagePath is not null ? fileStorage.GetPublicUrl(ev.Venue.ImagePath) : null,
                ev.Venue.Phone, ev.Venue.Website, ev.Venue.Latitude, ev.Venue.Longitude,
                ev.Venue.IsActive, ev.Venue.CreatedAt
            ),
            ev.OrganizerId,
            ev.Organizer?.Name,
            ev.TicketTypes.OrderBy(t => t.SortOrder).Select(t => new TicketTypeDto(
                t.Id, t.Name, t.Description, t.PriceCents,
                t.QuantityTotal, t.QuantitySold, t.QuantityTotal - t.QuantitySold, t.SortOrder
            )).ToList(),
            ev.CreatedAt
        ));
    }

    [HttpGet("{id:guid}/schema")]
    public async Task<IActionResult> GetSchemaOrg(Guid id)
    {
        var ev = await context.Events
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == EventStatus.Published);

        if (ev is null) return NotFound();

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var brandName = await settings.GetOrDefaultAsync("brand_name", "Code829");

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
                ["name"] = ev.Venue.Name,
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = ev.Venue.Address,
                    ["addressLocality"] = ev.Venue.City,
                    ["addressRegion"] = ev.Venue.State,
                    ["postalCode"] = ev.Venue.ZipCode,
                    ["addressCountry"] = "US"
                }
            },
            ["organizer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = brandName
            },
            ["offers"] = ev.TicketTypes.Select(t => new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["name"] = t.Name,
                ["price"] = (t.PriceCents / 100.0).ToString("F2"),
                ["priceCurrency"] = "USD",
                ["availability"] = t.QuantityTotal - t.QuantitySold > 0
                    ? "https://schema.org/InStock"
                    : "https://schema.org/SoldOut",
                ["url"] = $"{frontendUrl}/events/{ev.Slug}"
            }).ToList()
        };

        return Ok(schema);
    }
}
