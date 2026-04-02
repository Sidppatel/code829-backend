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
                tt.DefaultColor, tt.DefaultPriceCents, tt.PlatformFeeCents, tt.IsActive))
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
            tt.DefaultColor, tt.DefaultPriceCents, tt.PlatformFeeCents, tt.IsActive));
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
            tt.DefaultColor, tt.DefaultPriceCents, tt.PlatformFeeCents, tt.IsActive));
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

        var locked = await GetLockedTableIdsAsync(eventId);

        if (request.EditorMode is not null && Enum.TryParse<EditorMode>(request.EditorMode, true, out var mode))
            ev.EditorMode = mode;
        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();

        // Never delete locked tables
        var toRemove = existing.Where(t => !requestIds.Contains(t.Id) && !locked.Contains(t.Id));
        context.Tables.RemoveRange(toRemove);

        foreach (var rt in request.Tables)
        {
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);
            Enum.TryParse<PriceType>(rt.PriceType, true, out var priceType);

            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                // Skip locked tables — no modifications allowed
                if (locked.Contains(ex.Id)) continue;

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

        await SyncSeatsForEvent(eventId);
        return Ok(new { message = "Flushed to DB" });
    }

    /// <summary>
    /// Returns table IDs that have active holds or bookings — these cannot be modified/deleted.
    /// </summary>
    [HttpGet("admin/events/{eventId:guid}/layout/locked")]
    public async Task<IActionResult> GetLockedTables(Guid eventId)
    {
        var locked = await GetLockedTableIdsAsync(eventId);
        return Ok(new { lockedTableIds = locked });
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

        var locked = await GetLockedTableIdsAsync(eventId);

        if (request.EditorMode is not null && Enum.TryParse<EditorMode>(request.EditorMode, true, out var mode))
            ev.EditorMode = mode;
        ev.GridRows = request.GridRows;
        ev.GridCols = request.GridCols;

        var existing = await context.Tables.Where(t => t.EventId == eventId).ToListAsync();
        var requestIds = request.Tables
            .Where(t => !string.IsNullOrEmpty(t.Id) && Guid.TryParse(t.Id, out _))
            .Select(t => Guid.Parse(t.Id!))
            .ToHashSet();

        // Never delete locked tables
        var toRemove = existing.Where(t => !requestIds.Contains(t.Id) && !locked.Contains(t.Id));
        context.Tables.RemoveRange(toRemove);

        foreach (var rt in request.Tables)
        {
            Enum.TryParse<TableShape>(rt.Shape, true, out var shape);
            Enum.TryParse<PriceType>(rt.PriceType, true, out var priceType);

            var rtGuid = !string.IsNullOrEmpty(rt.Id) && Guid.TryParse(rt.Id, out var parsed) ? parsed : (Guid?)null;
            if (rtGuid.HasValue && existing.FirstOrDefault(e => e.Id == rtGuid.Value) is { } ex)
            {
                // Skip locked tables — no modifications allowed
                if (locked.Contains(ex.Id)) continue;

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
        await SyncSeatsForEvent(eventId);

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
        await SyncSeatsForEvent(eventId);
        return Created("", MapTable(table));
    }

    [HttpPut("admin/events/{eventId:guid}/layout/table/{tableId:guid}")]
    public async Task<IActionResult> UpdateTable(Guid eventId, Guid tableId, [FromBody] Contracts.DTOs.Layout.UpdateTableRequest request)
    {
        var table = await context.Tables.FirstOrDefaultAsync(t => t.Id == tableId && t.EventId == eventId);
        if (table is null) return NotFound(new { message = "Table not found" });

        var locked = await GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new { message = "This table has active holds or bookings and cannot be modified" });

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

        var locked = await GetLockedTableIdsAsync(eventId);
        if (locked.Contains(tableId))
            return BadRequest(new { message = "This table has active holds or bookings and cannot be deleted" });

        context.Seats.RemoveRange(table.Seats);
        context.Tables.Remove(table);
        await context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Returns layout tables with booking status for admin overview.
    /// Each table has: status (Available/Held/Booked), seatsSold count, and booking count.
    /// </summary>
    [HttpGet("admin/events/{eventId:guid}/layout/status")]
    public async Task<IActionResult> GetLayoutWithStatus(Guid eventId)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        var now = DateTime.UtcNow;

        var tables = await context.Tables
            .Include(t => t.TableType)
            .Include(t => t.Seats)
            .Where(t => t.EventId == eventId && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        // Get booked seat IDs
        var bookedSeatIds = await context.BookingItems
            .Where(bi => bi.SeatId.HasValue
                && bi.Booking.EventId == eventId
                && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn))
            .Select(bi => bi.SeatId!.Value)
            .ToHashSetAsync();

        // Get held seat IDs
        var heldSeatIds = await context.SeatHolds
            .Where(h => h.EventId == eventId && h.IsActive && h.ExpiresAt > now)
            .Select(h => h.SeatId)
            .ToHashSetAsync();

        // Get booking info per table — materialize first, then group in memory
        var bookedItems = await context.BookingItems
            .Where(bi => bi.SeatId.HasValue
                && bi.Booking.EventId == eventId
                && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn))
            .Select(bi => new {
                TableId = bi.Seat!.TableId,
                bi.BookingId,
                bi.Booking.UserId,
                BookerName = bi.Booking.User.FirstName + " " + bi.Booking.User.LastName
            })
            .ToListAsync();

        var bookingLookup = bookedItems
            .GroupBy(x => x.TableId)
            .ToDictionary(
                g => g.Key,
                g => new {
                    BookingCount = g.Select(x => x.BookingId).Distinct().Count(),
                    SeatsSold = g.Count(),
                    Bookers = g.DistinctBy(x => x.UserId).Select(x => x.BookerName).ToList()
                });

        var result = tables.Select(t =>
        {
            var seatIds = t.Seats.Select(s => s.Id).ToList();
            var isBooked = seatIds.Any(sid => bookedSeatIds.Contains(sid));
            var isHeld = seatIds.Any(sid => heldSeatIds.Contains(sid));
            var status = isBooked ? "Booked" : isHeld ? "Held" : "Available";

            bookingLookup.TryGetValue(t.Id, out var info);

            return new
            {
                t.Id,
                t.Label,
                Capacity = t.Capacity ?? 0,
                Shape = (t.Shape ?? Contracts.Enums.TableShape.Round).ToString(),
                t.Color,
                t.GridRow,
                t.GridCol,
                Status = status,
                SeatsSold = info?.SeatsSold ?? 0,
                BookingCount = info?.BookingCount ?? 0,
                Bookers = info?.Bookers ?? []
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
            .Select(t => new { t.Capacity, t.PriceType, t.PriceCents })
            .ToListAsync();

        var totalTables = tables.Count;
        var totalCapacity = tables.Sum(t => t.Capacity ?? 0);
        var totalPotentialRevenueCents = tables.Sum(t =>
            t.PriceType == PriceType.PerSeat
                ? (long)t.PriceCents * (t.Capacity ?? 0)
                : (long)t.PriceCents);

        var totalBookedRevenueCents = await context.BookingItems
            .Where(bi => bi.SeatId.HasValue
                && bi.Seat!.Table.EventId == eventId
                && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn))
            .SumAsync(bi => (long)bi.PriceCents);

        return Ok(new LayoutStatsResponse(
            totalTables, totalCapacity, totalPotentialRevenueCents, totalBookedRevenueCents));
    }

    [HttpPost("admin/events/{eventId:guid}/layout/bulk-insert")]
    public async Task<IActionResult> BulkInsertTables(Guid eventId, [FromBody] BulkInsertRequest request)
    {
        var ev = await context.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = "Event not found" });

        if ((ev.GridRows ?? 0) < 1 || (ev.GridCols ?? 0) < 1)
            return BadRequest(new { message = "Grid dimensions must be configured before bulk insert" });

        var rows = ev.GridRows!.Value;
        var cols = ev.GridCols!.Value;

        // Build occupation map
        var occupied = await context.Tables
            .Where(t => t.EventId == eventId && t.GridRow.HasValue && t.GridCol.HasValue)
            .Select(t => new { t.GridRow, t.GridCol })
            .ToListAsync();
        var occupiedSet = occupied.Select(o => (o.GridRow!.Value, o.GridCol!.Value)).ToHashSet();

        // Get already-placed type IDs for this event
        var placedTypeIds = await context.Tables
            .Where(t => t.EventId == eventId && t.TableTypeId.HasValue)
            .Select(t => t.TableTypeId!.Value)
            .Distinct()
            .ToListAsync();
        var placedSet = placedTypeIds.ToHashSet();

        // Filter to unlinked, active types
        var uniqueIds = request.TableTypeIds.Distinct().ToList();
        var types = await context.TableTypes
            .Where(tt => uniqueIds.Contains(tt.Id) && tt.IsActive)
            .ToListAsync();
        var unlinked = types.Where(tt => !placedSet.Contains(tt.Id)).ToList();

        // Find empty cells in row-major order
        var emptyCells = new Queue<(int row, int col)>();
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                if (!occupiedSet.Contains((r, c)))
                    emptyCells.Enqueue((r, c));

        // Collect existing labels to avoid unique constraint on (EventId, Label)
        var existingLabels = await context.Tables
            .Where(t => t.EventId == eventId)
            .Select(t => t.Label)
            .ToHashSetAsync();

        var inserted = new List<Table>();
        foreach (var tt in unlinked)
        {
            if (emptyCells.Count == 0) break;
            var (row, col) = emptyCells.Dequeue();

            // Auto-number label if duplicate
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
                PriceType = PriceType.PerTable,
                PriceCents = tt.DefaultPriceCents,
                IsActive = true,
                GridRow = row,
                GridCol = col,
                SortOrder = row * cols + col,
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
            await SyncSeatsForEvent(eventId);

            // Reload with TableType nav prop for mapping
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
            eventId, ev?.EditorMode?.ToString(), ev?.GridRows, ev?.GridCols,
            tables.Select(MapTable).ToList());
    }

    /// <summary>
    /// Returns table IDs that are locked: have active seat holds OR booked/checked-in booking items.
    /// These tables MUST NOT be modified or deleted.
    /// </summary>
    protected async Task<HashSet<Guid>> GetLockedTableIdsAsync(Guid eventId)
    {
        var now = DateTime.UtcNow;

        // Tables with active (non-expired) holds
        var heldTableIds = await context.SeatHolds
            .Where(h => h.EventId == eventId && h.IsActive && h.ExpiresAt > now)
            .Select(h => h.Seat.TableId)
            .Distinct()
            .ToListAsync();

        // Tables with booked seats (Paid or CheckedIn bookings)
        var bookedTableIds = await context.BookingItems
            .Where(bi => bi.SeatId.HasValue
                && bi.Booking.EventId == eventId
                && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn))
            .Select(bi => bi.Seat!.TableId)
            .Distinct()
            .ToListAsync();

        return heldTableIds.Concat(bookedTableIds).ToHashSet();
    }

    /// <summary>
    /// Ensures each table has exactly Capacity seat rows. Adds missing seats, removes excess
    /// (only if they have no active holds or booking items).
    /// </summary>
    protected async Task SyncSeatsForEvent(Guid eventId)
    {
        var tables = await context.Tables
            .Include(t => t.Seats)
            .Where(t => t.EventId == eventId && t.IsActive)
            .ToListAsync();

        foreach (var table in tables)
        {
            var capacity = table.Capacity ?? 0;
            var existing = table.Seats.OrderBy(s => s.SeatNumber).ToList();

            if (existing.Count < capacity)
            {
                for (var i = existing.Count + 1; i <= capacity; i++)
                {
                    context.Seats.Add(new Seat
                    {
                        Id = Guid.NewGuid(),
                        Label = $"S{i}",
                        SeatNumber = i,
                        TableId = table.Id
                    });
                }
            }
            else if (existing.Count > capacity)
            {
                var excess = existing.Skip(capacity).ToList();
                var excessIds = excess.Select(s => s.Id).ToHashSet();
                var hasHolds = await context.SeatHolds.AnyAsync(h => excessIds.Contains(h.SeatId) && h.IsActive);
                var hasBookings = await context.BookingItems.AnyAsync(bi => bi.SeatId.HasValue && excessIds.Contains(bi.SeatId.Value));
                if (!hasHolds && !hasBookings)
                    context.Seats.RemoveRange(excess);
            }
        }

        await context.SaveChangesAsync();
    }

    private static LayoutTableResponse MapTable(Table t) => new(
        t.Id, t.Label, t.Capacity ?? 0, (t.Shape ?? Contracts.Enums.TableShape.Round).ToString(), t.Color, t.Section,
        t.PriceType.ToString(), t.PriceCents, t.PriceOverrideCents, t.TableType?.PlatformFeeCents ?? 0, t.IsActive,
        t.GridRow, t.GridCol,
        t.SortOrder, t.TableTypeId, t.TableType?.Name);
}
