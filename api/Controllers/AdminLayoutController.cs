using Contracts.DTOs;
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

[ApiController]
[Authorize]
[RequireRole(UserRole.Admin)]
[Route("")]
public class AdminLayoutController(EventPlatformDbContext context, IConnectionMultiplexer redis) : ControllerBase
{
    // ═══════════════════════════════════════════════════════════
    //  Table Templates (global)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("admin/table-templates")]
    public async Task<IActionResult> GetTableTemplates()
    {
        var templates = await context.TableTemplates
            .OrderBy(tt => tt.Name)
            .Select(tt => new TableTemplateResponse(
                tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
                tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive))
            .ToListAsync();
        return Ok(templates);
    }

    [HttpPost("admin/table-templates")]
    public async Task<IActionResult> CreateTableTemplate([FromBody] CreateTableTemplateRequest request)
    {
        if (!Enum.TryParse<TableShape>(request.DefaultShape, true, out var shape))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        var tt = new TableTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DefaultCapacity = request.DefaultCapacity,
            DefaultShape = shape,
            DefaultColor = request.DefaultColor,
            DefaultPriceCents = request.DefaultPriceCents,
            IsActive = true
        };
        context.TableTemplates.Add(tt);
        await context.SaveChangesAsync();
        return Created("", new TableTemplateResponse(
            tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
            tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive));
    }

    [HttpPut("admin/table-templates/{id:guid}")]
    public async Task<IActionResult> UpdateTableTemplate(Guid id, [FromBody] CreateTableTemplateRequest request)
    {
        var tt = await context.TableTemplates.FindAsync(id);
        if (tt is null) return NotFound(new ApiError(404, "Table template not found", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<TableShape>(request.DefaultShape, true, out var shape))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        tt.Name = request.Name;
        tt.DefaultCapacity = request.DefaultCapacity;
        tt.DefaultShape = shape;
        tt.DefaultColor = request.DefaultColor;
        tt.DefaultPriceCents = request.DefaultPriceCents;
        if (request.IsActive.HasValue) tt.IsActive = request.IsActive.Value;
        tt.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(new TableTemplateResponse(
            tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
            tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive));
    }

    [HttpDelete("admin/table-templates/{id:guid}")]
    public async Task<IActionResult> DeleteTableTemplate(Guid id)
    {
        var tt = await context.TableTemplates.FindAsync(id);
        if (tt is null) return NotFound(new ApiError(404, "Table template not found", HttpContext.TraceIdentifier));

        tt.IsActive = false;
        tt.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════
    //  Event Tables (per-event table types)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("admin/events/{eventId:guid}/event-tables")]
    public async Task<IActionResult> GetEventTables(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        // Use summary view for read, but fall back to entity for full data including template name
        var eventTables = await context.EventTables
            .Include(et => et.TableTemplate)
            .Include(et => et.Tables)
            .Where(et => et.EventId == eventId)
            .OrderBy(et => et.Label)
            .ToListAsync();

        return Ok(eventTables.Select(MapEventTable).ToList());
    }

    [HttpPost("admin/events/{eventId:guid}/event-tables")]
    public async Task<IActionResult> CreateEventTable(Guid eventId, [FromBody] CreateEventTableRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        TableTemplate? template = null;
        if (request.TableTemplateId.HasValue)
        {
            template = await context.TableTemplates.FindAsync(request.TableTemplateId.Value);
            if (template is null) return NotFound(new ApiError(404, "Table template not found", HttpContext.TraceIdentifier));
        }

        // When no template, Label/Capacity/Shape are required
        if (template is null)
        {
            if (string.IsNullOrWhiteSpace(request.Label))
                return BadRequest(new ApiError(400, "Label is required when no template is selected", HttpContext.TraceIdentifier));
            if (request.Capacity is null || request.Capacity <= 0)
                return BadRequest(new ApiError(400, "Capacity is required when no template is selected", HttpContext.TraceIdentifier));
            if (string.IsNullOrWhiteSpace(request.Shape))
                return BadRequest(new ApiError(400, "Shape is required when no template is selected", HttpContext.TraceIdentifier));
        }

        var shapeStr = request.Shape ?? template?.DefaultShape.ToString() ?? "Square";
        if (!Enum.TryParse<TableShape>(shapeStr, true, out var shape))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        var et = new EventTable
        {
            Id = Guid.NewGuid(),
            Label = request.Label ?? template?.Name ?? "Custom Table",
            Capacity = request.Capacity ?? template?.DefaultCapacity ?? 4,
            Shape = shape,
            Color = request.Color ?? template?.DefaultColor,
            PriceCents = request.PriceCents ?? template?.DefaultPriceCents ?? 0,
            IsActive = true,
            EventId = eventId,
            TableTemplateId = template?.Id
        };
        context.EventTables.Add(et);
        await context.SaveChangesAsync();

        // Reload with nav properties
        var created = await context.EventTables
            .Include(x => x.TableTemplate)
            .Include(x => x.Tables)
            .FirstAsync(x => x.Id == et.Id);

        return Created("", MapEventTable(created));
    }

    [HttpPut("admin/events/{eventId:guid}/event-tables/{id:guid}")]
    public async Task<IActionResult> UpdateEventTable(Guid eventId, Guid id, [FromBody] UpdateEventTableRequest request)
    {
        var et = await context.EventTables
            .Include(x => x.TableTemplate)
            .Include(x => x.Tables)
            .FirstOrDefaultAsync(x => x.Id == id && x.EventId == eventId);
        if (et is null) return NotFound(new ApiError(404, "Event table not found", HttpContext.TraceIdentifier));

        // Block pricing changes if any tables have been sold or locked
        var tableIds = et.Tables.Select(t => t.Id).ToList();
        var hasActiveBookings = await context.Bookings.AnyAsync(b =>
            b.EventId == eventId && b.TableId.HasValue && tableIds.Contains(b.TableId.Value)
            && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Pending));
        var hasLockedTables = await context.Tables.AnyAsync(t =>
            t.EventTableId == id && t.Status == TableStatus.Locked && t.LockExpiresAt > DateTime.UtcNow);
        var hasSalesOrLocks = hasActiveBookings || hasLockedTables;

        if (hasSalesOrLocks)
        {
            // Block structural and pricing changes; only allow non-pricing updates
            if (request.PriceCents.HasValue || request.Capacity.HasValue)
                return BadRequest(new ApiError(400, "Cannot change pricing or capacity — tickets have been sold or locked", HttpContext.TraceIdentifier));
        }

        if (request.Shape is not null && !Enum.TryParse<TableShape>(request.Shape, true, out _))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        if (request.Label is not null) et.Label = request.Label;
        if (request.Capacity.HasValue) et.Capacity = request.Capacity.Value;
        if (request.Shape is not null && Enum.TryParse<TableShape>(request.Shape, true, out var shape)) et.Shape = shape;
        if (request.Color is not null) et.Color = request.Color;
        if (request.PriceCents.HasValue) et.PriceCents = request.PriceCents.Value;
        if (request.IsActive.HasValue) et.IsActive = request.IsActive.Value;
        et.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(MapEventTable(et));
    }

    [HttpDelete("admin/events/{eventId:guid}/event-tables/{id:guid}")]
    public async Task<IActionResult> DeleteEventTable(Guid eventId, Guid id)
    {
        var et = await context.EventTables
            .Include(x => x.Tables)
            .FirstOrDefaultAsync(x => x.Id == id && x.EventId == eventId);
        if (et is null) return NotFound(new ApiError(404, "Event table not found", HttpContext.TraceIdentifier));

        var tableIds = et.Tables.Select(t => t.Id).ToList();
        var hasActiveBookings = await context.Bookings.AnyAsync(b =>
            b.EventId == eventId && b.TableId.HasValue && tableIds.Contains(b.TableId.Value)
            && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Pending));
        if (hasActiveBookings)
            return BadRequest(new ApiError(400, "Cannot delete — tables have active bookings", HttpContext.TraceIdentifier));

        // Cascade delete table instances
        context.Tables.RemoveRange(et.Tables);
        context.EventTables.Remove(et);
        await context.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════
    //  Layout (table instances on grid)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> GetLayout(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return Ok(new EventLayoutResponse(
            eventId, ev.GridRows, ev.GridCols,
            tables.Select(MapTableViewToResponse).ToList()));
    }

    [HttpPost("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> SaveLayout(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var locked = await GetLockedTableIdsAsync(eventId);

        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();

        // Validate no duplicate grid cells
        var cells = new HashSet<(int, int)>();
        foreach (var rt in request.Tables)
        {
            if (!cells.Add((rt.GridRow, rt.GridCol)))
                return BadRequest(new ApiError(400, $"Duplicate grid cell ({rt.GridRow}, {rt.GridCol})", HttpContext.TraceIdentifier));
        }

        // Remove tables not in request (skip locked)
        var toRemove = existing.Where(t => !requestIds.Contains(t.Id) && !locked.Contains(t.Id));
        context.Tables.RemoveRange(toRemove);

        foreach (var rt in request.Tables)
        {
            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                if (locked.Contains(ex.Id)) continue;

                ex.Label = rt.Label;
                ex.GridRow = rt.GridRow;
                ex.GridCol = rt.GridCol;
                ex.IsActive = rt.IsActive;
                ex.SortOrder = rt.SortOrder;
                ex.EventTableId = rt.EventTableId;
                ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rtGuid ?? Guid.NewGuid(),
                    Label = rt.Label,
                    GridRow = rt.GridRow,
                    GridCol = rt.GridCol,
                    IsActive = rt.IsActive,
                    SortOrder = rt.SortOrder,
                    EventTableId = rt.EventTableId,
                    EventId = eventId
                });
            }
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Re-fetch with joined data
        var updatedTables = await context.Tables
            .Include(t => t.EventTable)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
        var updatedLocked = await GetLockedTableIdsAsync(eventId);
        return Ok(new EventLayoutResponse(
            eventId, ev.GridRows, ev.GridCols,
            updatedTables.Select(t => MapTableWithStatus(t, updatedLocked)).ToList()));
    }

    // ─── Redis Draft Endpoints ──────────────────────────────────

    private static string DraftKey(Guid eventId) => $"layout:draft:{eventId}";
    private static readonly TimeSpan DraftTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpPost("admin/events/{eventId:guid}/layout/draft")]
    public async Task<IActionResult> SaveDraft(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        // Drafts are ephemeral Redis data — allow saving even when some tables are locked.
        // Actual constraint enforcement happens in SaveLayout.
        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(request, JsonOpts);
        await db.StringSetAsync(DraftKey(eventId), json, DraftTtl);
        return Ok(new { message = "Draft saved" });
    }

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
        return Ok(new { source = "db", data = (SaveLayoutRequest?)null });
    }

    [HttpPost("admin/events/{eventId:guid}/layout/flush")]
    public async Task<IActionResult> FlushDraft(Guid eventId)
    {
        if (await IsLayoutLockedAsync(eventId))
            return Conflict(new ApiError(409, "Layout is locked — tables have active bookings", HttpContext.TraceIdentifier));

        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(DraftKey(eventId));
        if (!cached.HasValue)
            return Ok(new { message = "No draft to flush" });

        var request = JsonSerializer.Deserialize<SaveLayoutRequest>(cached.ToString(), JsonOpts);
        if (request is null)
            return Ok(new { message = "Invalid draft" });

        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var locked = await GetLockedTableIdsAsync(eventId);

        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();

        var toRemove = existing.Where(t => !requestIds.Contains(t.Id) && !locked.Contains(t.Id));
        context.Tables.RemoveRange(toRemove);

        foreach (var rt in request.Tables)
        {
            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                if (locked.Contains(ex.Id)) continue;

                ex.Label = rt.Label;
                ex.GridRow = rt.GridRow;
                ex.GridCol = rt.GridCol;
                ex.IsActive = rt.IsActive;
                ex.SortOrder = rt.SortOrder;
                ex.EventTableId = rt.EventTableId;
                ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rtGuid ?? Guid.NewGuid(),
                    Label = rt.Label,
                    GridRow = rt.GridRow,
                    GridCol = rt.GridCol,
                    IsActive = rt.IsActive,
                    SortOrder = rt.SortOrder,
                    EventTableId = rt.EventTableId,
                    EventId = eventId
                });
            }
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await db.KeyDeleteAsync(DraftKey(eventId));
        return Ok(new { message = "Flushed to DB" });
    }

    // ─── Single Table CRUD ──────────────────────────────────────

    [HttpPost("admin/events/{eventId:guid}/layout/table")]
    public async Task<IActionResult> AddTable(Guid eventId, [FromBody] AddTableRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var eventTable = await context.EventTables.FindAsync(request.EventTableId);
        if (eventTable is null || eventTable.EventId != eventId)
            return BadRequest(new ApiError(400, "Event table not found for this event", HttpContext.TraceIdentifier));

        var table = new Table
        {
            Id = Guid.NewGuid(),
            Label = request.Label,
            GridRow = request.GridRow,
            GridCol = request.GridCol,
            EventTableId = request.EventTableId,
            EventId = eventId
        };

        context.Tables.Add(table);
        await context.SaveChangesAsync();

        var created = await context.Tables
            .Include(t => t.EventTable)
            .FirstAsync(t => t.Id == table.Id);

        return Created("", MapTable(created));
    }

    [HttpPut("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> UpdateTable(Guid eventId, Guid tableId, [FromBody] Contracts.DTOs.Layout.UpdateTableRequest request)
    {
        var table = await context.Tables
            .Include(t => t.EventTable)
            .FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId);
        if (table is null) return NotFound(new ApiError(404, "Table not found", HttpContext.TraceIdentifier));

        var locked = await GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new ApiError(400, "This table has active bookings and cannot be modified", HttpContext.TraceIdentifier));

        if (request.Label is not null) table.Label = request.Label;
        if (request.GridRow.HasValue) table.GridRow = request.GridRow.Value;
        if (request.GridCol.HasValue) table.GridCol = request.GridCol.Value;
        if (request.IsActive.HasValue) table.IsActive = request.IsActive.Value;
        if (request.SortOrder.HasValue) table.SortOrder = request.SortOrder.Value;
        if (request.EventTableId.HasValue)
        {
            var et = await context.EventTables.FindAsync(request.EventTableId.Value);
            if (et is null || et.EventId != eventId)
                return BadRequest(new ApiError(400, "Event table not found for this event", HttpContext.TraceIdentifier));
            table.EventTableId = request.EventTableId.Value;
        }

        table.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Reload nav property if EventTableId changed
        await context.Entry(table).Reference(t => t.EventTable).LoadAsync();
        return Ok(MapTable(table));
    }

    [HttpDelete("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> DeleteTable(Guid eventId, Guid tableId)
    {
        var table = await context.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId);
        if (table is null) return NotFound(new ApiError(404, "Table not found", HttpContext.TraceIdentifier));

        var locked = await GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new ApiError(400, "This table has active bookings and cannot be deleted", HttpContext.TraceIdentifier));

        context.Tables.Remove(table);
        await context.SaveChangesAsync();
        return NoContent();
    }

    // ─── Status / Stats / Locked ────────────────────────────────

    [HttpGet("admin/events/{eventId:guid}/layout/status")]
    public async Task<IActionResult> GetLayoutWithStatus(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == eventId && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        var bookingInfo = await context.BookingViews.AsNoTracking()
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == "Paid" || b.Status == "CheckedIn"))
            .GroupBy(b => b.TableId!.Value)
            .Select(g => new
            {
                TableId = g.Key,
                BookingCount = g.Count(),
                SeatsBooked = g.Sum(b => b.SeatsReserved ?? 0)
            })
            .ToDictionaryAsync(x => x.TableId);

        var result = tables.Select(t =>
        {
            var status = t.Status == "Booked" ? "Booked"
                : t.Status == "Locked" ? "Held"
                : "Available";

            bookingInfo.TryGetValue(t.Id, out var info);

            return new
            {
                t.Id,
                t.Label,
                t.GridRow,
                t.GridCol,
                t.IsActive,
                t.SortOrder,
                t.EventTableId,
                EventTableLabel = t.EventTableLabel,
                t.Capacity,
                t.Shape,
                t.Color,
                t.PriceCents,
                Status = status,
                SeatsBooked = info?.SeatsBooked ?? 0,
                BookingCount = info?.BookingCount ?? 0
            };
        }).ToList();

        return Ok(new
        {
            eventId,
            ev.GridRows,
            ev.GridCols,
            Tables = result
        });
    }

    [HttpGet("admin/events/{eventId:guid}/layout/stats")]
    public async Task<IActionResult> GetLayoutStats(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        var stats = await context.TableViews.AsNoTracking()
            .Where(t => t.EventId == eventId && t.IsActive)
            .Select(t => new { t.Capacity, t.PriceCents })
            .ToListAsync();

        var totalTables = stats.Count;
        var totalCapacity = stats.Sum(s => s.Capacity);
        var totalPotentialRevenueCents = stats.Sum(s => (long)s.PriceCents);

        var totalBookedRevenueCents = await context.BookingViews.AsNoTracking()
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == "Paid" || b.Status == "CheckedIn"))
            .SumAsync(b => (long)b.SubtotalCents);

        return Ok(new LayoutStatsResponse(
            totalTables, totalCapacity, totalPotentialRevenueCents, totalBookedRevenueCents));
    }

    [HttpGet("admin/events/{eventId:guid}/layout/locked")]
    public async Task<IActionResult> GetLockedTables(Guid eventId)
    {
        var locked = await GetLockedTableIdsAsync(eventId);
        var layoutLocked = await IsLayoutLockedAsync(eventId);
        return Ok(new { layoutLocked, lockedTableIds = locked });
    }

    // ─── Bulk Insert (templates → event tables) ─────────────────

    [HttpPost("admin/events/{eventId:guid}/layout/bulk-insert")]
    public async Task<IActionResult> BulkInsertEventTables(Guid eventId, [FromBody] BulkInsertRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));

        // Find which templates already have an EventTable for this event
        var existingTemplateIds = await context.EventTables
            .Where(et => et.EventId == eventId && et.TableTemplateId.HasValue)
            .Select(et => et.TableTemplateId!.Value)
            .ToHashSetAsync();

        var uniqueIds = request.TableTemplateIds.Distinct().ToList();
        var templates = await context.TableTemplates
            .Where(tt => uniqueIds.Contains(tt.Id) && tt.IsActive)
            .ToListAsync();

        var newTemplates = templates.Where(tt => !existingTemplateIds.Contains(tt.Id)).ToList();

        var inserted = new List<EventTable>();
        foreach (var tt in newTemplates)
        {
            var et = new EventTable
            {
                Id = Guid.NewGuid(),
                Label = tt.Name,
                Capacity = tt.DefaultCapacity,
                Shape = tt.DefaultShape,
                Color = tt.DefaultColor,
                PriceCents = tt.DefaultPriceCents,
                IsActive = true,
                EventId = eventId,
                TableTemplateId = tt.Id
            };
            context.EventTables.Add(et);
            inserted.Add(et);
        }

        if (inserted.Count > 0)
        {
            await context.SaveChangesAsync();

            var insertedIds = inserted.Select(et => et.Id).ToHashSet();
            var reloaded = await context.EventTables
                .Include(et => et.TableTemplate)
                .Include(et => et.Tables)
                .Where(et => insertedIds.Contains(et.Id))
                .ToListAsync();

            return Ok(new BulkInsertResponse(reloaded.Count, reloaded.Select(MapEventTable).ToList()));
        }

        return Ok(new BulkInsertResponse(0, []));
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private async Task<bool> IsLayoutLockedAsync(Guid eventId)
    {
        return await context.Bookings.AnyAsync(b =>
            b.EventId == eventId && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Refunded);
    }

    protected async Task<HashSet<Guid>> GetLockedTableIdsAsync(Guid eventId)
    {
        var bookingLockedIds = await context.Bookings
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Pending))
            .Select(b => b.TableId!.Value)
            .Distinct()
            .ToListAsync();

        var holdLockedIds = await context.Tables
            .Where(t => t.EventId == eventId
                && t.Status == TableStatus.Locked
                && t.LockExpiresAt > DateTime.UtcNow)
            .Select(t => t.Id)
            .ToListAsync();

        return bookingLockedIds.Union(holdLockedIds).ToHashSet();
    }

    private static LayoutTableResponse MapTable(Table t) => new(
        t.Id, t.Label, t.GridRow, t.GridCol, t.IsActive,
        t.SortOrder, t.EventTableId, t.EventTable.Label,
        t.EventTable.Capacity, t.EventTable.Shape.ToString(),
        t.EventTable.Color, t.EventTable.PriceCents);

    private static LayoutTableResponse MapTableViewToResponse(Db.Entities.Views.TableView t) => new(
        t.Id, t.Label, t.GridRow, t.GridCol, t.IsActive,
        t.SortOrder, t.EventTableId, t.EventTableLabel,
        t.Capacity, t.Shape, t.Color, t.PriceCents,
        t.Status);

    private static LayoutTableResponse MapTableWithStatus(Table t, HashSet<Guid> lockedIds)
    {
        var status = lockedIds.Contains(t.Id)
            ? (t.Status == TableStatus.Booked ? "Booked"
                : t.Status == TableStatus.Locked ? "Locked"
                : "Booked")
            : "Available";

        return new LayoutTableResponse(
            t.Id, t.Label, t.GridRow, t.GridCol, t.IsActive,
            t.SortOrder, t.EventTableId, t.EventTable.Label,
            t.EventTable.Capacity, t.EventTable.Shape.ToString(),
            t.EventTable.Color, t.EventTable.PriceCents,
            status);
    }

    private static EventTableResponse MapEventTable(EventTable et) => new(
        et.Id, et.Label, et.Capacity, et.Shape.ToString(),
        et.Color, et.PriceCents, et.IsActive,
        et.EventId, et.TableTemplateId,
        et.TableTemplate?.Name,
        et.Tables.Count);
}
