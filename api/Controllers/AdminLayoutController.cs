using Api.Middleware;
using Contracts.DTOs.Layout;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Event-scoped layout editor endpoints for grid and canvas modes.
/// Also serves global table type templates.
/// </summary>
[ApiController]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminLayoutController(EventPlatformDbContext context) : ControllerBase
{
    [HttpGet("admin/table-types")]
    public async Task<IActionResult> GetTableTypes()
    {
        var types = await context.TableTypes
            .Where(tt => tt.IsActive)
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
        var requestIds = request.Tables.Where(t => t.Id.HasValue).Select(t => t.Id!.Value).ToHashSet();

        context.Tables.RemoveRange(existing.Where(t => !requestIds.Contains(t.Id)));

        foreach (var rt in request.Tables)
        {
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);
            Enum.TryParse<PriceType>(rt.PriceType, true, out var priceType);

            if (rt.Id.HasValue && existing.FirstOrDefault(e => e.Id == rt.Id.Value) is { } ex)
            {
                ex.Label = rt.Label; ex.Capacity = rt.Capacity; ex.Shape = shape;
                ex.Color = rt.Color; ex.Section = rt.Section; ex.PriceType = priceType;
                ex.PriceCents = rt.PriceCents; ex.PriceOverrideCents = rt.PriceOverrideCents;
                ex.IsActive = rt.IsActive; ex.GridRow = rt.GridRow; ex.GridCol = rt.GridCol;
                ex.PosX = rt.PosX; ex.PosY = rt.PosY; ex.Width = rt.Width;
                ex.Height = rt.Height; ex.Rotation = rt.Rotation; ex.SortOrder = rt.SortOrder;
                ex.TableTypeId = rt.TableTypeId; ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rt.Id ?? Guid.NewGuid(), Label = rt.Label, Capacity = rt.Capacity,
                    Shape = shape, Color = rt.Color, Section = rt.Section, PriceType = priceType,
                    PriceCents = rt.PriceCents, PriceOverrideCents = rt.PriceOverrideCents,
                    IsActive = rt.IsActive, GridRow = rt.GridRow, GridCol = rt.GridCol,
                    PosX = rt.PosX, PosY = rt.PosY, Width = rt.Width, Height = rt.Height,
                    Rotation = rt.Rotation, SortOrder = rt.SortOrder, TableTypeId = rt.TableTypeId,
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
            PosX = request.PosX, PosY = request.PosY, Width = request.Width,
            Height = request.Height, Rotation = request.Rotation,
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
        if (request.PosX.HasValue) table.PosX = request.PosX;
        if (request.PosY.HasValue) table.PosY = request.PosY;
        if (request.Width.HasValue) table.Width = request.Width.Value;
        if (request.Height.HasValue) table.Height = request.Height.Value;
        if (request.Rotation.HasValue) table.Rotation = request.Rotation.Value;

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
        t.GridRow, t.GridCol, t.PosX, t.PosY, t.Width, t.Height, t.Rotation,
        t.SortOrder, t.TableTypeId, t.TableType?.Name);
}
