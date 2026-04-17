using System.Security.Claims;
using Contracts.DTOs;
using System.Text.Json;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Layout;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Controllers;

[ApiController]
[Authorize]
[RequireRole(UserRole.Admin)]
[Route("")]
public class AdminLayoutController(
    EventPlatformDbContext context,
    ILayoutProcedures layoutProc,
    ITableProcedures tableProc,
    IConnectionMultiplexer redis,
    ISettingsService settings) : ControllerBase
{
    // ═══════════════════════════════════════════════════════════
    //  Table Templates (global)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("admin/table-templates")]
    public async Task<IActionResult> GetTableTemplates()
    {
        var templates = await layoutProc.ListTableTemplatesAsync();
        return Ok(templates.Select(tt => new TableTemplateResponse(
            tt.Id, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
            tt.DefaultColor, tt.DefaultPriceCents, tt.IsActive)).ToList());
    }

    [HttpPost("admin/table-templates")]
    public async Task<IActionResult> CreateTableTemplate([FromBody] CreateTableTemplateRequest request)
    {
        if (!Enum.TryParse<TableShape>(request.DefaultShape, true, out var shape))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        var id = await layoutProc.CreateTableTemplateAsync(
            request.Name, request.DefaultCapacity, shape.ToString(),
            request.DefaultColor, request.DefaultPriceCents);

        return Created("", new TableTemplateResponse(
            id, request.Name, request.DefaultCapacity, shape.ToString(),
            request.DefaultColor, request.DefaultPriceCents, true));
    }

    [HttpPut("admin/table-templates/{id:guid}")]
    public async Task<IActionResult> UpdateTableTemplate(Guid id, [FromBody] CreateTableTemplateRequest request)
    {
        var tt = await layoutProc.GetTableTemplateByIdAsync(id);
        if (tt is null) return NotFound(new ApiError(404, "Table template not found", HttpContext.TraceIdentifier));

        if (!Enum.TryParse<TableShape>(request.DefaultShape, true, out var shape))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        await layoutProc.UpdateTableTemplateAsync(
            id, request.Name, request.DefaultCapacity, shape.ToString(),
            request.DefaultColor, request.DefaultPriceCents, request.IsActive);

        var updated = await layoutProc.GetTableTemplateByIdAsync(id);
        return Ok(new TableTemplateResponse(
            updated!.Id, updated.Name, updated.DefaultCapacity, updated.DefaultShape.ToString(),
            updated.DefaultColor, updated.DefaultPriceCents, updated.IsActive));
    }

    [HttpDelete("admin/table-templates/{id:guid}")]
    public async Task<IActionResult> DeleteTableTemplate(Guid id)
    {
        var tt = await layoutProc.GetTableTemplateByIdAsync(id);
        if (tt is null) return NotFound(new ApiError(404, "Table template not found", HttpContext.TraceIdentifier));

        await layoutProc.DeactivateTableTemplateAsync(id);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════
    //  Event Tables (per-event table types)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("admin/events/{eventId:guid}/event-tables")]
    public async Task<IActionResult> GetEventTables(Guid eventId)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var eventTables = await layoutProc.ListEventTablesForEventAsync(eventId);
        return Ok((await MapEventTables(eventTables, eventId)).ToList());
    }

    [HttpPost("admin/events/{eventId:guid}/event-tables")]
    public async Task<IActionResult> CreateEventTable(Guid eventId, [FromBody] CreateEventTableRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        TableTemplate? template = null;
        if (request.TableTemplateId.HasValue)
        {
            template = await layoutProc.GetTableTemplateByIdAsync(request.TableTemplateId.Value);
            if (template is null) return NotFound(new ApiError(404, "Table template not found", HttpContext.TraceIdentifier));
        }

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

        var defaultGridFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_grid_cents", "2500") ?? "2500");

        var label = request.Label ?? template?.Name ?? "Custom Table";
        var capacity = request.Capacity ?? template?.DefaultCapacity ?? 4;
        var color = request.Color ?? template?.DefaultColor;
        var priceCents = request.PriceCents ?? template?.DefaultPriceCents ?? 0;

        var etId = await tableProc.CreateEventTableAsync(
            eventId, label, capacity, shape.ToString(), color,
            priceCents, defaultGridFee, template?.Id);

        return Created("", new EventTableResponse(
            etId, label, capacity, shape.ToString(), color, priceCents, true,
            eventId, template?.Id, template?.Name, 0));
    }

    [HttpPut("admin/events/{eventId:guid}/event-tables/{id:guid}")]
    public async Task<IActionResult> UpdateEventTable(Guid eventId, Guid id, [FromBody] UpdateEventTableRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var et = await layoutProc.GetEventTableByIdAsync(id);
        if (et is null || et.EventId != eventId)
            return NotFound(new ApiError(404, "Event table not found", HttpContext.TraceIdentifier));

        var hasActiveBookings = await layoutProc.EventTableHasActiveBookingsAsync(eventId, id);
        var hasLockedTables = await layoutProc.EventTableHasLockedTablesAsync(id);
        var hasSalesOrLocks = hasActiveBookings || hasLockedTables;

        if (hasSalesOrLocks)
        {
            if (request.PriceCents.HasValue || request.Capacity.HasValue)
                return BadRequest(new ApiError(400, "Cannot change pricing or capacity — tickets have been sold or locked", HttpContext.TraceIdentifier));
        }

        if (request.Shape is not null && !Enum.TryParse<TableShape>(request.Shape, true, out _))
            return BadRequest(new ApiError(400, "Invalid shape", HttpContext.TraceIdentifier));

        string? shapeOut = null;
        if (request.Shape is not null && Enum.TryParse<TableShape>(request.Shape, true, out var parsedShape))
            shapeOut = parsedShape.ToString();

        await layoutProc.UpdateEventTableAsync(
            id, request.Label, request.Capacity, shapeOut,
            request.Color, request.PriceCents, request.IsActive);

        var updated = await layoutProc.GetEventTableByIdAsync(id);
        return Ok((await MapEventTables([updated!], eventId)).First());
    }

    [HttpDelete("admin/events/{eventId:guid}/event-tables/{id:guid}")]
    public async Task<IActionResult> DeleteEventTable(Guid eventId, Guid id)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var et = await layoutProc.GetEventTableByIdAsync(id);
        if (et is null || et.EventId != eventId)
            return NotFound(new ApiError(404, "Event table not found", HttpContext.TraceIdentifier));

        if (await layoutProc.EventTableHasActiveBookingsAsync(eventId, id))
            return BadRequest(new ApiError(400, "Cannot delete — tables have active bookings", HttpContext.TraceIdentifier));

        await layoutProc.DeleteEventTableAsync(id);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════
    //  Layout (table instances on grid)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> GetLayout(Guid eventId)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var tables = await context_TableViews_ForEvent(eventId);
        return Ok(new EventLayoutResponse(
            eventId, ev.GridRows, ev.GridCols,
            tables.Select(MapTableViewToResponse).ToList()));
    }

    [HttpPost("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> SaveLayout(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var locked = await layoutProc.GetLockedTableIdsAsync(eventId);

        var cells = new HashSet<(int, int)>();
        foreach (var rt in request.Tables)
        {
            if (!cells.Add((rt.GridRow, rt.GridCol)))
                return BadRequest(new ApiError(400, $"Duplicate grid cell ({rt.GridRow}, {rt.GridCol})", HttpContext.TraceIdentifier));
        }

        var tablesJson = SerializeTablesForSave(request.Tables);
        await layoutProc.SaveEventLayoutAsync(eventId, request.GridRows, request.GridCols, tablesJson, [.. locked]);

        var updatedTables = await context_TableViews_ForEvent(eventId);
        var updatedLocked = await layoutProc.GetLockedTableIdsAsync(eventId);
        return Ok(new EventLayoutResponse(
            eventId, request.GridRows, request.GridCols,
            updatedTables.Select(t => MapTableViewWithStatus(t, updatedLocked)).ToList()));
    }

    // ─── Redis Draft Endpoints ──────────────────────────────────

    private static string DraftKey(Guid eventId) => $"layout:draft:{eventId}";
    private static readonly TimeSpan DraftTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpPost("admin/events/{eventId:guid}/layout/draft")]
    public async Task<IActionResult> SaveDraft(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(request, JsonOpts);
        await db.StringSetAsync(DraftKey(eventId), json, DraftTtl);
        return Ok(new { message = "Draft saved" });
    }

    [HttpGet("admin/events/{eventId:guid}/layout/draft")]
    public async Task<IActionResult> LoadDraft(Guid eventId)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

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
        if (await layoutProc.EventHasActiveBookingsAsync(eventId))
            return Conflict(new ApiError(409, "Layout is locked — tables have active bookings", HttpContext.TraceIdentifier));

        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(DraftKey(eventId));
        if (!cached.HasValue)
            return Ok(new { message = "No draft to flush" });

        var request = JsonSerializer.Deserialize<SaveLayoutRequest>(cached.ToString(), JsonOpts);
        if (request is null)
            return Ok(new { message = "Invalid draft" });

        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var locked = await layoutProc.GetLockedTableIdsAsync(eventId);
        var tablesJson = SerializeTablesForSave(request.Tables);
        await layoutProc.SaveEventLayoutAsync(eventId, request.GridRows, request.GridCols, tablesJson, [.. locked]);

        await db.KeyDeleteAsync(DraftKey(eventId));
        return Ok(new { message = "Flushed to DB" });
    }

    // ─── Single Table CRUD ──────────────────────────────────────

    [HttpPost("admin/events/{eventId:guid}/layout/table")]
    public async Task<IActionResult> AddTable(Guid eventId, [FromBody] AddTableRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var eventTable = await layoutProc.GetEventTableByIdAsync(request.EventTableId);
        if (eventTable is null || eventTable.EventId != eventId)
            return BadRequest(new ApiError(400, "Event table not found for this event", HttpContext.TraceIdentifier));

        var tableId = await tableProc.CreateTableAsync(
            request.EventTableId, eventId, request.Label,
            request.GridRow, request.GridCol, 0);

        return Created("", new LayoutTableResponse(
            tableId, request.Label, request.GridRow, request.GridCol, true,
            0, request.EventTableId, eventTable.Label,
            eventTable.Capacity, eventTable.Shape.ToString(),
            eventTable.Color, eventTable.PriceCents));
    }

    [HttpPut("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> UpdateTable(Guid eventId, Guid tableId, [FromBody] Contracts.DTOs.Layout.UpdateTableRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var table = await layoutProc.GetTableByIdAsync(tableId);
        if (table is null || table.EventId != eventId)
            return NotFound(new ApiError(404, "Table not found", HttpContext.TraceIdentifier));

        var locked = await layoutProc.GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new ApiError(400, "This table has active bookings and cannot be modified", HttpContext.TraceIdentifier));

        if (request.EventTableId.HasValue)
        {
            var et = await layoutProc.GetEventTableByIdAsync(request.EventTableId.Value);
            if (et is null || et.EventId != eventId)
                return BadRequest(new ApiError(400, "Event table not found for this event", HttpContext.TraceIdentifier));
        }

        await layoutProc.UpdateTableAsync(
            tableId, request.Label, request.EventTableId,
            request.GridRow, request.GridCol, request.IsActive, request.SortOrder);

        var updated = await layoutProc.GetTableByIdAsync(tableId);
        var eventTable = await layoutProc.GetEventTableByIdAsync(updated!.EventTableId);

        return Ok(new LayoutTableResponse(
            updated.Id, updated.Label, updated.GridRow, updated.GridCol, updated.IsActive,
            updated.SortOrder, updated.EventTableId, eventTable!.Label,
            eventTable.Capacity, eventTable.Shape.ToString(),
            eventTable.Color, eventTable.PriceCents));
    }

    [HttpDelete("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> DeleteTable(Guid eventId, Guid tableId)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var table = await layoutProc.GetTableByIdAsync(tableId);
        if (table is null || table.EventId != eventId)
            return NotFound(new ApiError(404, "Table not found", HttpContext.TraceIdentifier));

        var locked = await layoutProc.GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new ApiError(400, "This table has active bookings and cannot be deleted", HttpContext.TraceIdentifier));

        await layoutProc.DeleteTableAsync(tableId);
        return NoContent();
    }

    // ─── Status / Stats / Locked ────────────────────────────────

    [HttpGet("admin/events/{eventId:guid}/layout/status")]
    public async Task<IActionResult> GetLayoutWithStatus(Guid eventId)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var tables = await context_TableViews_ForEvent(eventId, activeOnly: true);
        var bookingInfo = await GetBookingInfoForEvent(eventId);

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
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var tables = await context_TableViews_ForEvent(eventId, activeOnly: true);
        var totalTables = tables.Count;
        var totalCapacity = tables.Sum(t => t.Capacity);
        var totalPotentialRevenueCents = tables.Sum(t => (long)t.PriceCents);

        var bookingInfo = await GetBookingInfoForEvent(eventId);
        var totalBookedRevenueCents = bookingInfo.Values.Sum(x => x.SubtotalCents);

        return Ok(new LayoutStatsResponse(
            totalTables, totalCapacity, totalPotentialRevenueCents, totalBookedRevenueCents));
    }

    [HttpGet("admin/events/{eventId:guid}/layout/locked")]
    public async Task<IActionResult> GetLockedTables(Guid eventId)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var locked = await layoutProc.GetLockedTableIdsAsync(eventId);
        var layoutLocked = await layoutProc.EventHasActiveBookingsAsync(eventId);
        return Ok(new { layoutLocked, lockedTableIds = locked });
    }

    // ─── Bulk Insert (templates → event tables) ─────────────────

    [HttpPost("admin/events/{eventId:guid}/layout/bulk-insert")]
    public async Task<IActionResult> BulkInsertEventTables(Guid eventId, [FromBody] BulkInsertRequest request)
    {
        var ev = await layoutProc.GetEventByIdForLayoutAsync(eventId);
        if (ev is null) return NotFound(new ApiError(404, "Event not found", HttpContext.TraceIdentifier));
        if (!IsOwnerOrDeveloper(ev.OrganizerId)) return Forbid();

        var existingTemplateIds = await layoutProc.ListExistingEventTableTemplateIdsAsync(eventId);
        var uniqueIds = request.TableTemplateIds.Distinct().ToList();
        var templates = await layoutProc.ListActiveTableTemplatesByIdsAsync(uniqueIds);
        var newTemplates = templates.Where(tt => !existingTemplateIds.Contains(tt.Id)).ToList();

        var defaultGridFee = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_grid_cents", "2500") ?? "2500");

        var created = new List<EventTableResponse>();
        foreach (var tt in newTemplates)
        {
            var etId = await tableProc.CreateEventTableAsync(
                eventId, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
                tt.DefaultColor, tt.DefaultPriceCents, defaultGridFee, tt.Id);
            created.Add(new EventTableResponse(
                etId, tt.Name, tt.DefaultCapacity, tt.DefaultShape.ToString(),
                tt.DefaultColor, tt.DefaultPriceCents, true,
                eventId, tt.Id, tt.Name, 0));
        }

        return Ok(new BulkInsertResponse(created.Count, created));
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private Guid GetCurrentUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsOwnerOrDeveloper(Guid organizerId) =>
        organizerId == GetCurrentUserId()
        || User.IsInRole(UserRole.Developer.ToString())
        || User.IsInRole(UserRole.Admin.ToString());

    private async Task<List<Db.Entities.Views.TableView>> context_TableViews_ForEvent(Guid eventId, bool activeOnly = false)
    {
        var query = context.TableViews.AsNoTracking().Where(t => t.EventId == eventId);
        if (activeOnly) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.SortOrder).ToListAsync();
    }

    private record TableBookingInfo(int BookingCount, int SeatsBooked, long SubtotalCents);

    private async Task<Dictionary<Guid, TableBookingInfo>> GetBookingInfoForEvent(Guid eventId)
    {
        var grouped = await context.BookingViews.AsNoTracking()
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == "Paid" || b.Status == "CheckedIn"))
            .GroupBy(b => b.TableId!.Value)
            .Select(g => new
            {
                TableId = g.Key,
                BookingCount = g.Count(),
                SeatsBooked = g.Sum(b => b.SeatsReserved ?? 0),
                SubtotalCents = g.Sum(b => (long)b.SubtotalCents)
            })
            .ToListAsync();
        return grouped.ToDictionary(x => x.TableId, x => new TableBookingInfo(x.BookingCount, x.SeatsBooked, x.SubtotalCents));
    }

    private async Task<List<EventTableResponse>> MapEventTables(IEnumerable<EventTable> eventTables, Guid eventId)
    {
        var list = eventTables.ToList();
        if (list.Count == 0) return [];

        var templateIds = list.Where(et => et.TableTemplateId.HasValue).Select(et => et.TableTemplateId!.Value).Distinct().ToList();
        var templates = templateIds.Count > 0
            ? await layoutProc.ListActiveTableTemplatesByIdsAsync(templateIds)
            : [];
        var templateMap = templates.ToDictionary(t => t.Id, t => t.Name);

        var tables = await layoutProc.ListTablesForEventAsync(eventId);
        var tableCountByEventTable = tables.GroupBy(t => t.EventTableId).ToDictionary(g => g.Key, g => g.Count());

        return list.Select(et => new EventTableResponse(
            et.Id, et.Label, et.Capacity, et.Shape.ToString(),
            et.Color, et.PriceCents, et.IsActive,
            et.EventId, et.TableTemplateId,
            et.TableTemplateId.HasValue && templateMap.TryGetValue(et.TableTemplateId.Value, out var name) ? name : null,
            tableCountByEventTable.GetValueOrDefault(et.Id, 0))).ToList();
    }

    private static LayoutTableResponse MapTableViewToResponse(Db.Entities.Views.TableView t) => new(
        t.Id, t.Label, t.GridRow, t.GridCol, t.IsActive,
        t.SortOrder, t.EventTableId, t.EventTableLabel,
        t.Capacity, t.Shape, t.Color, t.PriceCents,
        t.Status);

    private static LayoutTableResponse MapTableViewWithStatus(Db.Entities.Views.TableView t, HashSet<Guid> lockedIds)
    {
        var status = lockedIds.Contains(t.Id)
            ? (t.Status == "Booked" ? "Booked"
                : t.Status == "Locked" ? "Locked"
                : "Booked")
            : "Available";

        return new LayoutTableResponse(
            t.Id, t.Label, t.GridRow, t.GridCol, t.IsActive,
            t.SortOrder, t.EventTableId, t.EventTableLabel,
            t.Capacity, t.Shape, t.Color, t.PriceCents,
            status);
    }

    private static string SerializeTablesForSave(List<SaveLayoutTableRequest> tables)
    {
        var serializable = tables.Select(t => new
        {
            Id = !string.IsNullOrEmpty(t.Id) ? t.Id : null,
            t.Label,
            t.GridRow,
            t.GridCol,
            t.IsActive,
            t.SortOrder,
            EventTableId = t.EventTableId.ToString()
        });
        return JsonSerializer.Serialize(serializable);
    }
}
