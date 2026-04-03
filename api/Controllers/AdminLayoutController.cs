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
public class AdminLayoutController(EventPlatformDbContext context, IConnectionMultiplexer redis) : ControllerBase
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

    // --- Redis Draft Endpoints ---

    private static string DraftKey(Guid eventId) => $"layout:draft:{eventId}";
    private static readonly TimeSpan DraftTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpPost("admin/events/{eventId:guid}/layout/draft")]
    public async Task<IActionResult> SaveDraft(Guid eventId, [FromBody] SaveLayoutRequest request)
    {
        if (await IsLayoutLockedAsync(eventId))
            return Conflict(new { message = "Layout is locked — tables have active bookings" });

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
            return Conflict(new { message = "Layout is locked — tables have active bookings" });

        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(DraftKey(eventId));
        if (!cached.HasValue)
            return Ok(new { message = "No draft to flush" });

        var request = JsonSerializer.Deserialize<SaveLayoutRequest>(cached.ToString(), JsonOpts);
        if (request is null)
            return Ok(new { message = "Invalid draft" });

        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

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
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);

            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                if (locked.Contains(ex.Id)) continue;

                ex.Label = rt.Label; ex.Capacity = rt.Capacity; ex.Shape = shape;
                ex.Color = rt.Color; ex.PriceCents = rt.PriceCents;
                ex.IsActive = rt.IsActive; ex.PosX = rt.PosX; ex.PosY = rt.PosY;
                ex.SortOrder = rt.SortOrder;
                ex.TableTypeId = rt.TableTypeId; ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rtGuid ?? Guid.NewGuid(), Label = rt.Label, Capacity = rt.Capacity,
                    Shape = shape, Color = rt.Color, PriceCents = rt.PriceCents,
                    IsActive = rt.IsActive, PosX = rt.PosX, PosY = rt.PosY,
                    SortOrder = rt.SortOrder, TableTypeId = rt.TableTypeId,
                    EventId = eventId, VenueId = ev.VenueId
                });
            }
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await db.KeyDeleteAsync(DraftKey(eventId));
        return Ok(new { message = "Flushed to DB" });
    }

    [HttpGet("admin/events/{eventId:guid}/layout/locked")]
    public async Task<IActionResult> GetLockedTables(Guid eventId)
    {
        var locked = await GetLockedTableIdsAsync(eventId);
        var layoutLocked = await IsLayoutLockedAsync(eventId);
        return Ok(new { layoutLocked, lockedTableIds = locked });
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

        var lockedIds = await GetLockedTableIdsAsync(eventId);

        return Ok(new EventLayoutResponse(
            eventId, ev.GridRows, ev.GridCols,
            tables.Select(t => MapTableWithStatus(t, lockedIds)).ToList()));
    }

    [HttpPost("admin/events/{eventId:guid}/layout")]
    public async Task<IActionResult> SaveLayout(Guid eventId, [FromBody] SaveLayoutRequest request)
    {

        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var locked = await GetLockedTableIdsAsync(eventId);

        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();

        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lt in existing.Where(t => locked.Contains(t.Id) && requestIds.Contains(t.Id)))
            usedLabels.Add(lt.Label);

        var toRemove = existing.Where(t => !requestIds.Contains(t.Id) && !locked.Contains(t.Id));
        context.Tables.RemoveRange(toRemove);

        foreach (var rt in request.Tables)
        {
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);

            var rawLabel = (rt.Label ?? "Table").Length > 20 ? rt.Label![..20] : rt.Label ?? "Table";
            var label = rawLabel;
            var counter = 1;
            while (usedLabels.Contains(label))
            {
                counter++;
                var suffix = $" {counter}";
                var maxBase = 20 - suffix.Length;
                var basePart = rawLabel.Length > maxBase ? rawLabel[..maxBase] : rawLabel;
                label = basePart + suffix;
            }
            usedLabels.Add(label);

            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                if (locked.Contains(ex.Id)) continue;

                ex.Label = label; ex.Capacity = rt.Capacity; ex.Shape = shape;
                ex.Color = rt.Color; ex.PriceCents = rt.PriceCents;
                ex.IsActive = rt.IsActive; ex.PosX = rt.PosX; ex.PosY = rt.PosY;
                ex.SortOrder = rt.SortOrder;
                ex.TableTypeId = rt.TableTypeId; ex.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.Tables.Add(new Table
                {
                    Id = rtGuid ?? Guid.NewGuid(), Label = label, Capacity = rt.Capacity,
                    Shape = shape, Color = rt.Color, PriceCents = rt.PriceCents,
                    IsActive = rt.IsActive, PosX = rt.PosX, PosY = rt.PosY,
                    SortOrder = rt.SortOrder, TableTypeId = rt.TableTypeId,
                    EventId = eventId, VenueId = ev.VenueId
                });
            }
        }

        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Re-fetch with status
        var ev2 = await context.Events.FindAsync(eventId);
        var updatedTables = await context.Tables
            .Include(t => t.TableType)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
        var updatedLocked = await GetLockedTableIdsAsync(eventId);
        return Ok(new EventLayoutResponse(
            eventId, ev2?.GridRows, ev2?.GridCols,
            updatedTables.Select(t => MapTableWithStatus(t, updatedLocked)).ToList()));
    }

    [HttpPost("admin/events/{eventId:guid}/layout/table")]
    public async Task<IActionResult> AddTable(Guid eventId, [FromBody] AddTableRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        Enum.TryParse<TableShape>(request.Shape, true, out var shape);

        var table = new Table
        {
            Id = Guid.NewGuid(), Label = request.Label, Capacity = request.Capacity,
            Shape = shape, Color = request.Color, PriceCents = request.PriceCents,
            PosX = request.PosX, PosY = request.PosY,
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

        var locked = await GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new { message = "This table has active bookings and cannot be modified" });

        if (request.Label is not null) table.Label = request.Label;
        if (request.Capacity.HasValue) table.Capacity = request.Capacity.Value;
        if (request.Shape is not null && Enum.TryParse<TableShape>(request.Shape, true, out var s)) table.Shape = s;
        if (request.Color is not null) table.Color = request.Color;
        if (request.PriceCents.HasValue) table.PriceCents = request.PriceCents.Value;
        if (request.IsActive.HasValue) table.IsActive = request.IsActive.Value;
        if (request.PosX.HasValue) table.PosX = request.PosX.Value;
        if (request.PosY.HasValue) table.PosY = request.PosY.Value;
        if (request.SortOrder.HasValue) table.SortOrder = request.SortOrder.Value;

        table.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(MapTable(table));
    }

    [HttpDelete("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> DeleteTable(Guid eventId, Guid tableId)
    {
        var table = await context.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId);
        if (table is null) return NotFound(new { message = "Table not found" });

        var locked = await GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new { message = "This table has active bookings and cannot be deleted" });

        context.Tables.Remove(table);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("admin/events/{eventId:guid}/layout/status")]
    public async Task<IActionResult> GetLayoutWithStatus(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var tables = await context.Tables
            .Include(t => t.TableType)
            .Where(t => t.EventId == eventId && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        // Get booking info per table
        var bookingInfo = await context.Bookings
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn))
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
            var status = t.Status == TableStatus.Booked ? "Booked"
                : t.Status == TableStatus.Locked ? "Held"
                : "Available";

            bookingInfo.TryGetValue(t.Id, out var info);

            return new
            {
                t.Id,
                t.Label,
                t.Capacity,
                Shape = t.Shape.ToString(),
                t.Color,
                t.PosX,
                t.PosY,
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
        if (ev is null) return NotFound(new { message = "Event not found" });

        var tables = await context.Tables
            .Where(t => t.EventId == eventId && t.IsActive)
            .Select(t => new { t.Capacity, t.PriceCents })
            .ToListAsync();

        var totalTables = tables.Count;
        var totalCapacity = tables.Sum(t => t.Capacity);
        var totalPotentialRevenueCents = tables.Sum(t => (long)t.PriceCents);

        var totalBookedRevenueCents = await context.Bookings
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn))
            .SumAsync(b => (long)b.SubtotalCents);

        return Ok(new LayoutStatsResponse(
            totalTables, totalCapacity, totalPotentialRevenueCents, totalBookedRevenueCents));
    }

    [HttpPost("admin/events/{eventId:guid}/layout/bulk-insert")]
    public async Task<IActionResult> BulkInsertTables(Guid eventId, [FromBody] BulkInsertRequest request)
    {
        if (await IsLayoutLockedAsync(eventId))
            return Conflict(new { message = "Layout is locked — tables have active bookings" });

        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var existingLabels = await context.Tables
            .Where(t => t.EventId == eventId)
            .Select(t => t.Label)
            .ToHashSetAsync();

        var placedTypeIds = await context.Tables
            .Where(t => t.EventId == eventId && t.TableTypeId.HasValue)
            .Select(t => t.TableTypeId!.Value)
            .Distinct()
            .ToListAsync();
        var placedSet = placedTypeIds.ToHashSet();

        var uniqueIds = request.TableTypeIds.Distinct().ToList();
        var types = await context.TableTypes
            .Where(tt => uniqueIds.Contains(tt.Id) && tt.IsActive)
            .ToListAsync();
        var unlinked = types.Where(tt => !placedSet.Contains(tt.Id)).ToList();

        var inserted = new List<Table>();
        var sortOrder = await context.Tables
            .Where(t => t.EventId == eventId)
            .Select(t => t.SortOrder)
            .DefaultIfEmpty(0)
            .MaxAsync();

        foreach (var tt in unlinked)
        {
            sortOrder++;
            var baseName = tt.Name.Length > 16 ? tt.Name[..16] : tt.Name;
            var label = baseName;
            var counter = 1;
            while (existingLabels.Contains(label))
            {
                counter++;
                label = $"{baseName} {counter}";
            }
            existingLabels.Add(label);

            var table = new Table
            {
                Id = Guid.NewGuid(),
                Label = label,
                Capacity = tt.DefaultCapacity,
                Shape = tt.DefaultShape,
                Color = tt.DefaultColor,
                PriceCents = tt.DefaultPriceCents,
                IsActive = true,
                PosX = 0, PosY = 0,
                SortOrder = sortOrder,
                TableTypeId = tt.Id,
                EventId = eventId,
                VenueId = ev.VenueId
            };
            context.Tables.Add(table);
            inserted.Add(table);
        }

        if (inserted.Count > 0)
        {
            await context.SaveChangesAsync();

            var insertedIds = inserted.Select(t => t.Id).ToHashSet();
            var reloaded = await context.Tables
                .Include(t => t.TableType)
                .Where(t => insertedIds.Contains(t.Id))
                .ToListAsync();

            return Ok(new BulkInsertResponse(reloaded.Count, reloaded.Select(MapTable).ToList()));
        }

        return Ok(new BulkInsertResponse(0, []));
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
            eventId, ev?.GridRows, ev?.GridCols,
            tables.Select(MapTable).ToList());
    }

    private async Task<bool> IsLayoutLockedAsync(Guid eventId)
    {
        return await context.Bookings.AnyAsync(b =>
            b.EventId == eventId && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Refunded);
    }

    protected async Task<HashSet<Guid>> GetLockedTableIdsAsync(Guid eventId)
    {
        var lockedTableIds = await context.Bookings
            .Where(b => b.EventId == eventId && b.TableId.HasValue
                && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Pending))
            .Select(b => b.TableId!.Value)
            .Distinct()
            .ToListAsync();

        return lockedTableIds.ToHashSet();
    }

    private static LayoutTableResponse MapTable(Table t) => new(
        t.Id, t.Label, t.Capacity, t.Shape.ToString(), t.Color,
        t.PriceCents, t.IsActive,
        t.PosX, t.PosY,
        t.SortOrder, t.TableTypeId, t.TableType?.Name);

    private static LayoutTableResponse MapTableWithStatus(Table t, HashSet<Guid> lockedIds)
    {
        var status = lockedIds.Contains(t.Id)
            ? (t.Status == TableStatus.Booked ? "Booked"
                : t.Status == TableStatus.Locked ? "Locked"
                : "Booked") // If in lockedIds (has active booking) but status not set, treat as Booked
            : "Available";

        return new LayoutTableResponse(
            t.Id, t.Label, t.Capacity, t.Shape.ToString(), t.Color,
            t.PriceCents, t.IsActive,
            t.PosX, t.PosY,
            t.SortOrder, t.TableTypeId, t.TableType?.Name,
            status);
    }
}
