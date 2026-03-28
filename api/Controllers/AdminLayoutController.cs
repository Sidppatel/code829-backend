using Api.Middleware;
using Contracts.DTOs.Seats;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("admin/venues/{venueId:guid}/layout")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminLayoutController(EventPlatformDbContext context) : ControllerBase
{
    /// <summary>
    /// List table types for a venue.
    /// </summary>
    [HttpGet("table-types")]
    public async Task<IActionResult> GetTableTypes(Guid venueId)
    {
        var types = await context.TableTypes
            .Where(tt => tt.VenueId == venueId)
            .Select(tt => new TableTypeDto(tt.Id, tt.Name, tt.SeatsPerTable, tt.Shape, tt.WidthPx, tt.HeightPx))
            .ToListAsync();
        return Ok(types);
    }

    /// <summary>
    /// Create a table type for a venue.
    /// </summary>
    [HttpPost("table-types")]
    public async Task<IActionResult> CreateTableType(Guid venueId, [FromBody] CreateTableTypeRequest request)
    {
        var venue = await context.Venues.FindAsync(venueId);
        if (venue is null) return NotFound(new { message = "Venue not found" });

        var tt = new TableType
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            SeatsPerTable = request.SeatsPerTable,
            Shape = request.Shape,
            WidthPx = request.WidthPx,
            HeightPx = request.HeightPx,
            VenueId = venueId
        };
        context.TableTypes.Add(tt);
        await context.SaveChangesAsync();
        return Created("", new TableTypeDto(tt.Id, tt.Name, tt.SeatsPerTable, tt.Shape, tt.WidthPx, tt.HeightPx));
    }

    /// <summary>
    /// Create a table in a venue. Auto-generates seats based on the table type's SeatsPerTable.
    /// </summary>
    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable(Guid venueId, [FromBody] CreateTableRequest request)
    {
        var tableType = await context.TableTypes.FindAsync(request.TableTypeId);
        if (tableType is null || tableType.VenueId != venueId)
            return BadRequest(new { message = "Table type not found for this venue" });

        var table = new Table
        {
            Id = Guid.NewGuid(),
            Label = request.Label,
            X = request.X,
            Y = request.Y,
            Rotation = request.Rotation,
            TableTypeId = request.TableTypeId,
            VenueId = venueId
        };
        context.Tables.Add(table);

        // Auto-generate seats
        for (var i = 1; i <= tableType.SeatsPerTable; i++)
        {
            context.Seats.Add(new Seat
            {
                Id = Guid.NewGuid(),
                Label = $"{request.Label}-S{i}",
                SeatNumber = i,
                TableId = table.Id
            });
        }

        await context.SaveChangesAsync();

        var seats = await context.Seats
            .Where(s => s.TableId == table.Id)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync();

        return Created("", new TableDto(
            table.Id, table.Label, table.X, table.Y, table.Rotation,
            tableType.Name, tableType.Shape, tableType.WidthPx, tableType.HeightPx,
            seats.Select(s => new SeatDto(s.Id, s.Label, s.SeatNumber, table.Id, table.Label, false, null)).ToList()
        ));
    }

    /// <summary>
    /// Update a table's position/label.
    /// </summary>
    [HttpPut("tables/{tableId:guid}")]
    public async Task<IActionResult> UpdateTable(Guid venueId, Guid tableId, [FromBody] UpdateTableRequest request)
    {
        var table = await context.Tables.FirstOrDefaultAsync(t => t.Id == tableId && t.VenueId == venueId);
        if (table is null) return NotFound(new { message = "Table not found" });

        if (request.Label is not null) table.Label = request.Label;
        if (request.X.HasValue) table.X = request.X.Value;
        if (request.Y.HasValue) table.Y = request.Y.Value;
        if (request.Rotation.HasValue) table.Rotation = request.Rotation.Value;

        table.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(new { message = "Table updated" });
    }

    /// <summary>
    /// Delete a table and its seats.
    /// </summary>
    [HttpDelete("tables/{tableId:guid}")]
    public async Task<IActionResult> DeleteTable(Guid venueId, Guid tableId)
    {
        var table = await context.Tables
            .Include(t => t.Seats)
            .FirstOrDefaultAsync(t => t.Id == tableId && t.VenueId == venueId);
        if (table is null) return NotFound(new { message = "Table not found" });

        context.Seats.RemoveRange(table.Seats);
        context.Tables.Remove(table);
        await context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Get full layout for a venue (all tables + seats).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLayout(Guid venueId)
    {
        var venue = await context.Venues.FindAsync(venueId);
        if (venue is null) return NotFound(new { message = "Venue not found" });

        var tables = await context.Tables
            .Include(t => t.TableType)
            .Include(t => t.Seats)
            .Where(t => t.VenueId == venueId)
            .OrderBy(t => t.Label)
            .ToListAsync();

        return Ok(new VenueLayoutDto(
            venue.Id, venue.Name,
            tables.Select(t => new TableDto(
                t.Id, t.Label, t.X, t.Y, t.Rotation,
                t.TableType.Name, t.TableType.Shape,
                t.TableType.WidthPx, t.TableType.HeightPx,
                t.Seats.OrderBy(s => s.SeatNumber).Select(s =>
                    new SeatDto(s.Id, s.Label, s.SeatNumber, t.Id, t.Label, false, null)
                ).ToList()
            )).ToList()
        ));
    }
}
