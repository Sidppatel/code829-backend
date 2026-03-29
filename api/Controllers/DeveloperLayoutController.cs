using System.Text.Json;
using Api.Middleware;
using Contracts.DTOs.Layout;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Controllers;

/// <summary>
/// Event-scoped layout editor endpoints for grid and canvas modes.
/// Layout drafts are cached in Redis for instant saves; DB writes happen
/// on explicit save or page navigation (via the flush endpoint).
/// </summary>
[ApiController]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperLayoutController(EventPlatformDbContext context, IConnectionMultiplexer redis) : ControllerBase
{
    [HttpGet("admin/table-types")]
    public async Task<IActionResult> GetTableTypes()
    {
        var types = await context.TableTypes
            .OrderBy(tt => tt.Name)
            .Select(tt => new TableTypeResponse(
                tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
                tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive))
            .ToListAsync();
        return Ok(types);
    }

    [HttpPost("admin/table-types")]
    public async Task<IActionResult> CreateTableType([FromBody] CreateTableTypeRequest request)
    {
        if (!Enum.TryParse<TableShape>(request.DefaultShape, true, out var shape))
            return BadRequest(new { message = "Invalid shape" });

        var tt = new TableType
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DefaultCapacity = request.DefaultCapacity,
            DefaultShape = shape,
            DefaultColor = request.DefaultColor,
            DefaultPriceCents = request.DefaultPriceCents,
            IsActive = true
        };
        context.TableTypes.Add(tt);
        await context.SaveChangesAsync();
        return Created("", new TableTypeResponse(
            tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
            tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive));
    }

    [HttpPut("admin/table-types/{id:guid}")]
    public async Task<IActionResult> UpdateTableType(Guid id, [FromBody] CreateTableTypeRequest request)
    {
        var tt = await context.TableTypes.FindAsync(id);
        if (tt is null) return NotFound(new { message = "Table type not found" });

        if (!Enum.TryParse<TableShape>(request.DefaultShape, true, out var shape))
            return BadRequest(new { message = "Invalid shape" });

        tt.Name = request.Name;
        tt.DefaultCapacity = request.DefaultCapacity;
        tt.DefaultShape = shape;
        tt.DefaultColor = request.DefaultColor;
        tt.DefaultPriceCents = request.DefaultPriceCents;
        tt.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(new TableTypeResponse(
            tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
            tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive));
    }

    [HttpDelete("admin/table-types/{id:guid}")]
    public async Task<IActionResult> DeleteTableType(Guid id)
    {
        var tt = await context.TableTypes.FindAsync(id);
        if (tt is null) return NotFound(new { message = "Table type not found" });

        tt.IsActive = false;
        tt.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return NoContent();
    }

    // ─── Redis Draft Endpoints ──────────────────────────────────────────

    private static string DraftKey(Guid eventId) => $"layout:draft:{eventId}";
    private static readonly TimeSpan DraftTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Save layout draft to Redis (instant, no DB write).
    /// Called on every change in the editor.
    /// </summary>
    [HttpPost("admin/events/{eventId:guid}/layout/draft")]
    public async Task<IActionResult> SaveDraft(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(request, JsonOpts);
        await db.StringSetAsync(DraftKey(eventId), json, DraftTtl);
        return Ok(new { message = "Draft saved" });
    }

    /// <summary>
    /// Load layout draft from Redis. Falls back to DB if no draft exists.
    /// </summary>
    [HttpGet("admin/events/{eventId:guid}/layout/draft")]
    public async Task<IActionResult> LoadDraft(Guid eventId)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(DraftKey(eventId));
        if (cached.HasValue)
        {
            var draft = JsonSerializer.Deserialize<SaveLayoutRequest>(cached.ToString(), JsonOpts);
            return Ok(new { source = "redis", data = draft });
        }
        // Fall back to DB
        return Ok(new { source = "db", data = (SaveLayoutRequest?)null });
    }

    /// <summary>
    /// Flush: write Redis draft to DB, then clear the draft.
    /// Called when user navigates away from the editor.
    /// </summary>
    [HttpPost("admin/events/{eventId:guid}/layout/flush")]
    public async Task<IActionResult> FlushDraft(Guid eventId)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(DraftKey(eventId));
        if (!cached.HasValue)
            return Ok(new { message = "No draft to flush" });

        var request = JsonSerializer.Deserialize<SaveLayoutRequest>(cached.ToString(), JsonOpts);
        if (request is null)
            return Ok(new { message = "Invalid draft" });

        // Write to DB using existing SaveLayout logic
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        if (request.EditorMode is not null && Enum.TryParse<EditorMode>(request.EditorMode, true, out var mode))
            ev.EditorMode = mode;
        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();
        context.Tables.RemoveRange(existing.Where(t => !requestIds.Contains(t.Id)));

        foreach (var rt in request.Tables)
        {
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);
            Enum.TryParse<PriceType>(rt.PriceType, true, out var priceType);

            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                ex.Label = rt.Label; ex.Capacity = rt.Capacity; ex.Shape = shape;
                ex.Color = rt.Color; ex.Section = rt.Section; ex.PriceType = priceType;
                ex.PriceCents = rt.PriceCents; ex.PriceOverrideCents = rt.PriceOverrideCents;
                ex.IsActive = rt.IsActive; ex.GridRow = rt.GridRow; ex.GridCol = rt.GridCol;
                
                ex.SortOrder = rt.SortOrder;
                ex.TableTypeId = rt.TableTypeId; ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rtGuid ?? Guid.NewGuid(), Label = rt.Label, Capacity = rt.Capacity,
                    Shape = shape, Color = rt.Color, Section = rt.Section, PriceType = priceType,
                    PriceCents = rt.PriceCents, PriceOverrideCents = rt.PriceOverrideCents,
                    IsActive = rt.IsActive, GridRow = rt.GridRow, GridCol = rt.GridCol,
                    
                    SortOrder = rt.SortOrder, TableTypeId = rt.TableTypeId,
                    EventId = eventId, VenueId = ev.VenueId
                });
            }
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Clear the Redis draft
        await db.KeyDeleteAsync(DraftKey(eventId));

        return Ok(new { message = "Flushed to DB" });
    }

    [HttpGet("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> GetLayout(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var tables = await context.Tables
            .Include(t => t.TableType)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return Ok(new EventLayoutResponse(
            eventId, ev.EditorMode?.ToString(), ev.GridRows, ev.GridCols,
            tables.Select(MapTable).ToList()));
    }

    [HttpPost("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> SaveLayout(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        if (request.EditorMode is not null && Enum.TryParse<EditorMode>(request.EditorMode, true, out var mode))
            ev.EditorMode = mode;
        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();

        context.Tables.RemoveRange(existing.Where(t => !requestIds.Contains(t.Id)));

        foreach (var rt in request.Tables)
        {
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);
            Enum.TryParse<PriceType>(rt.PriceType, true, out var priceType);

            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                ex.Label = rt.Label; ex.Capacity = rt.Capacity; ex.Shape = shape;
                ex.Color = rt.Color; ex.Section = rt.Section; ex.PriceType = priceType;
                ex.PriceCents = rt.PriceCents; ex.PriceOverrideCents = rt.PriceOverrideCents;
                ex.IsActive = rt.IsActive; ex.GridRow = rt.GridRow; ex.GridCol = rt.GridCol;
                
                ex.SortOrder = rt.SortOrder;
                ex.TableTypeId = rt.TableTypeId; ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rtGuid ?? Guid.NewGuid(), Label = rt.Label, Capacity = rt.Capacity,
                    Shape = shape, Color = rt.Color, Section = rt.Section, PriceType = priceType,
                    PriceCents = rt.PriceCents, PriceOverrideCents = rt.PriceOverrideCents,
                    IsActive = rt.IsActive, GridRow = rt.GridRow, GridCol = rt.GridCol,
                    
                    SortOrder = rt.SortOrder, TableTypeId = rt.TableTypeId,
                    EventId = eventId, VenueId = ev.VenueId
                });
            }
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(await GetLayoutInternal(eventId));
    }

    [HttpPost("admin/events/{eventId:guid}/layout/table")]
    public async Task<IActionResult> AddTable(Guid eventId, [FromBody] AddTableRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        Enum.TryParse<TableShape>(request.Shape, true, out var shape);
        Enum.TryParse<PriceType>(request.PriceType, true, out var priceType);

        var table = new Table
        {
            Id = Guid.NewGuid(), Label = request.Label, Capacity = request.Capacity,
            Shape = shape, Color = request.Color, Section = request.Section,
            PriceType = priceType, PriceCents = request.PriceCents,
            GridRow = request.GridRow, GridCol = request.GridCol,
            
            
            TableTypeId = request.TableTypeId, EventId = eventId, VenueId = ev.VenueId
        };

        context.Tables.Add(table);
        await context.SaveChangesAsync();
        return Created("", MapTable(table));
    }

    [HttpPut("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> UpdateTable(Guid eventId, Guid tableId, [FromBody] Contracts.DTOs.Layout.UpdateTableRequest request)
    {
        var table = await context.Tables.FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId);
        if (table is null) return NotFound(new { message = "Table not found" });

        if (request.Label is not null) table.Label = request.Label;
        if (request.Capacity.HasValue) table.Capacity = request.Capacity.Value;
        if (request.Shape is not null && Enum.TryParse<TableShape>(request.Shape, true, out var s)) table.Shape = s;
        if (request.Color is not null) table.Color = request.Color;
        if (request.Section is not null) table.Section = request.Section;
        if (request.PriceType is not null && Enum.TryParse<PriceType>(request.PriceType, true, out var pt)) table.PriceType = pt;
        if (request.PriceCents.HasValue) table.PriceCents = request.PriceCents.Value;
        if (request.IsActive.HasValue) table.IsActive = request.IsActive.Value;
        if (request.GridRow.HasValue) table.GridRow = request.GridRow;
        if (request.GridCol.HasValue) table.GridCol = request.GridCol;

        table.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapTable(table));
    }

    [HttpDelete("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> DeleteTable(Guid eventId, Guid tableId)
    {
        var table = await context.Tables
            .Include(t => t.Seats)
            .FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId);
        if (table is null) return NotFound(new { message = "Table not found" });

        context.Seats.RemoveRange(table.Seats);
        context.Tables.Remove(table);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<EventLayoutResponse> GetLayoutInternal(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        var tables = await context.Tables
            .Include(t => t.TableType)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
        return new EventLayoutResponse(
            eventId, ev?.EditorMode?.ToString(), ev?.GridRows, ev?.GridCols,
            tables.Select(MapTable).ToList());
    }

    private static LayoutTableResponse MapTable(Table t) => new(
        t.Id, t.Label, t.Capacity, t.Shape.ToString(), t.Color, t.Section,
        t.PriceType.ToString(), t.PriceCents, t.PriceOverrideCents, t.IsActive,
        t.GridRow, t.GridCol,
        t.SortOrder, t.TableTypeId, t.TableType?.Name);
}

