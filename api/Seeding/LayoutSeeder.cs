using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds EventTables and Table instances for Grid events.
/// 3-tier model: TableTemplate → EventTable → Table.
/// EventTables define table types per event (with pricing).
/// Tables are individual instances placed on the grid (GridRow/GridCol).
/// Runs after VenueEventSeeder and DataSeeder.
/// </summary>
public static class LayoutSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();

        if (await context.EventTables.AnyAsync())
            return;

        var gridEvents = await context.Events
            .Where(e => e.LayoutMode == LayoutMode.Grid && e.Status == EventStatus.Published)
            .ToListAsync();

        if (gridEvents.Count == 0) return;

        var templates = await context.TableTemplates.ToListAsync();
        if (templates.Count == 0) return;

        var roundTemplate = templates.First(t => t.DefaultShape == TableShape.Round);
        var rectTemplate = templates.First(t => t.DefaultShape == TableShape.Rectangle);
        var cocktailTemplate = templates.First(t => t.DefaultShape == TableShape.Cocktail);
        var squareTemplate = templates.First(t => t.DefaultShape == TableShape.Square);

        foreach (var ev in gridEvents)
        {
            SeedEventTablesAndInstances(context, ev, roundTemplate, rectTemplate, cocktailTemplate, squareTemplate);
        }

        await context.SaveChangesAsync();
        Log.Information("[Seed] Created event tables and table instances for {Count} grid events", gridEvents.Count);
    }

    private static void SeedEventTablesAndInstances(
        EventPlatformDbContext context, Event ev,
        TableTemplate round, TableTemplate rect, TableTemplate cocktail, TableTemplate square)
    {
        var layout = GetEventLayout(ev, round, rect, cocktail, square);

        // Create EventTable records for this event
        var eventTableMap = new Dictionary<string, EventTable>();
        foreach (var etDef in layout.EventTableDefs)
        {
            var eventTable = new EventTable
            {
                Id = Guid.NewGuid(),
                Label = etDef.Label,
                Capacity = etDef.Capacity,
                Shape = etDef.Shape,
                Color = etDef.Template.DefaultColor,
                PriceCents = etDef.PriceCents,
                IsActive = true,
                EventId = ev.Id,
                TableTemplateId = etDef.Template.Id
            };
            context.EventTables.Add(eventTable);
            eventTableMap[etDef.Key] = eventTable;
        }

        // Create Table instances on the grid
        var sortOrder = 0;
        foreach (var tableDef in layout.TableDefs)
        {
            var eventTable = eventTableMap[tableDef.EventTableKey];
            context.Tables.Add(new Table
            {
                Id = Guid.NewGuid(),
                Label = tableDef.Label,
                GridRow = tableDef.Row,
                GridCol = tableDef.Col,
                SortOrder = sortOrder++,
                IsActive = true,
                Status = TableStatus.Available,
                EventTableId = eventTable.Id,
                EventId = ev.Id
            });
        }
    }

    private static EventLayout GetEventLayout(
        Event ev, TableTemplate round, TableTemplate rect, TableTemplate cocktail, TableTemplate square)
    {
        return ev.Title switch
        {
            var t when t.Contains("Gala") && t.Contains("Bellingrath") => BellingrathGalaLayout(ev, round, rect, cocktail),
            var t when t.Contains("Farm-to-Table") => FarmToTableLayout(ev, round, rect, cocktail),
            var t when t.Contains("Luncheon") => LuncheonLayout(ev, round, rect),
            var t when t.Contains("Comedy") => ComedyNightLayout(ev, round, rect, cocktail),
            var t when t.Contains("Wine & Dine") => WineDineLayout(ev, round, rect, square),
            _ => DefaultGridLayout(ev, round, rect, cocktail),
        };
    }

    // ── Bellingrath Gardens Spring Gala (6 rows x 8 cols) ──────────────
    private static EventLayout BellingrathGalaLayout(Event ev, TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("vip", "VIP Table", 6, TableShape.Rectangle, rect, 15000),
            new("standard", "Standard Table", 4, TableShape.Round, round, 7500),
            new("back", "Back Row Table", 4, TableShape.Round, round, 5000),
            new("cocktail", "Cocktail High-Top", 2, TableShape.Cocktail, cocktail, 3000),
        };

        var tables = new List<TableInstanceDef>();
        // VIP front row (row 0)
        tables.Add(new("A1", 0, 1, "vip"));
        tables.Add(new("A2", 0, 3, "vip"));
        tables.Add(new("A3", 0, 5, "vip"));
        // Standard middle rows (rows 2-3)
        tables.Add(new("B1", 2, 0, "standard"));
        tables.Add(new("B2", 2, 2, "standard"));
        tables.Add(new("B3", 2, 4, "standard"));
        tables.Add(new("B4", 2, 6, "standard"));
        tables.Add(new("B5", 3, 1, "standard"));
        tables.Add(new("B6", 3, 3, "standard"));
        tables.Add(new("B7", 3, 5, "standard"));
        tables.Add(new("B8", 3, 7, "standard"));
        // Back rows (row 4)
        tables.Add(new("C1", 4, 0, "back"));
        tables.Add(new("C2", 4, 2, "back"));
        tables.Add(new("C3", 4, 4, "back"));
        tables.Add(new("C4", 4, 6, "back"));
        // Cocktail high-tops (row 5)
        tables.Add(new("D1", 5, 1, "cocktail"));
        tables.Add(new("D2", 5, 3, "cocktail"));
        tables.Add(new("D3", 5, 5, "cocktail"));
        tables.Add(new("D4", 5, 7, "cocktail"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    // ── Farm-to-Table Dinner (4 rows x 6 cols) ────────────────────────
    private static EventLayout FarmToTableLayout(Event ev, TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("chef", "Chef's Table", 6, TableShape.Rectangle, rect, 12000),
            new("garden", "Garden Table", 4, TableShape.Round, round, 8500),
            new("cocktail", "Herb Garden High-Top", 2, TableShape.Cocktail, cocktail, 4500),
        };

        var tables = new List<TableInstanceDef>();
        // Chef's table front center (row 0)
        tables.Add(new("Chef1", 0, 2, "chef"));
        // Garden tables (rows 1-2)
        tables.Add(new("G1", 1, 0, "garden"));
        tables.Add(new("G2", 1, 2, "garden"));
        tables.Add(new("G3", 1, 4, "garden"));
        tables.Add(new("P1", 2, 1, "garden"));
        tables.Add(new("P2", 2, 3, "garden"));
        tables.Add(new("P3", 2, 5, "garden"));
        // Cocktail high-tops (row 3)
        tables.Add(new("H1", 3, 0, "cocktail"));
        tables.Add(new("H2", 3, 2, "cocktail"));
        tables.Add(new("H3", 3, 4, "cocktail"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    // ── Mobile Business Leaders Luncheon (5 rows x 8 cols) ─────────────
    private static EventLayout LuncheonLayout(Event ev, TableTemplate round, TableTemplate rect)
    {
        var eventTables = new EventTableDef[]
        {
            new("head", "Head Table", 8, TableShape.Rectangle, rect, 10000),
            new("standard", "Standard Table", 4, TableShape.Round, round, 3500),
        };

        var tables = new List<TableInstanceDef>();
        // Head table (row 0)
        tables.Add(new("Head", 0, 3, "head"));
        // Standard tables (rows 1-3)
        tables.Add(new("S1", 1, 0, "standard"));
        tables.Add(new("S2", 1, 2, "standard"));
        tables.Add(new("S3", 1, 4, "standard"));
        tables.Add(new("S4", 1, 6, "standard"));
        tables.Add(new("S5", 2, 1, "standard"));
        tables.Add(new("S6", 2, 3, "standard"));
        tables.Add(new("S7", 2, 5, "standard"));
        tables.Add(new("S8", 2, 7, "standard"));
        tables.Add(new("S9", 3, 0, "standard"));
        tables.Add(new("S10", 3, 3, "standard"));
        tables.Add(new("S11", 3, 6, "standard"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    // ── Blind Mule Comedy Night (4 rows x 5 cols) ──────────────────────
    private static EventLayout ComedyNightLayout(Event ev, TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("front", "Front Row Table", 4, TableShape.Rectangle, rect, 5000),
            new("middle", "Middle Table", 4, TableShape.Round, round, 3000),
            new("back", "Bar High-Top", 2, TableShape.Cocktail, cocktail, 1500),
        };

        var tables = new List<TableInstanceDef>();
        // Front row (row 0)
        tables.Add(new("F1", 0, 1, "front"));
        tables.Add(new("F2", 0, 2, "front"));
        tables.Add(new("F3", 0, 3, "front"));
        // Middle rows (rows 1-2)
        tables.Add(new("M1", 1, 0, "middle"));
        tables.Add(new("M2", 1, 2, "middle"));
        tables.Add(new("M3", 1, 4, "middle"));
        tables.Add(new("M4", 2, 1, "middle"));
        tables.Add(new("M5", 2, 3, "middle"));
        // Back row (row 3)
        tables.Add(new("B1", 3, 0, "back"));
        tables.Add(new("B2", 3, 1, "back"));
        tables.Add(new("B3", 3, 3, "back"));
        tables.Add(new("B4", 3, 4, "back"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    // ── Fairhope Wine & Dine Gala (5 rows x 6 cols) ───────────────────
    private static EventLayout WineDineLayout(Event ev, TableTemplate round, TableTemplate rect, TableTemplate square)
    {
        var eventTables = new EventTableDef[]
        {
            new("vip", "VIP Wine Table", 6, TableShape.Rectangle, rect, 12000),
            new("standard", "Dining Table", 4, TableShape.Round, round, 8000),
            new("value", "Garden Table", 4, TableShape.Round, round, 6000),
            new("lounge", "Lounge Section", 8, TableShape.Square, square, 15000),
        };

        var tables = new List<TableInstanceDef>();
        // VIP tables (row 0)
        tables.Add(new("V1", 0, 1, "vip"));
        tables.Add(new("V2", 0, 4, "vip"));
        // Standard dining (rows 1-2)
        tables.Add(new("R1", 1, 0, "standard"));
        tables.Add(new("R2", 1, 2, "standard"));
        tables.Add(new("R3", 1, 4, "standard"));
        tables.Add(new("R4", 2, 1, "standard"));
        // Value garden tables (row 3)
        tables.Add(new("R5", 3, 0, "value"));
        tables.Add(new("R6", 3, 2, "value"));
        tables.Add(new("R7", 3, 4, "value"));
        // Lounge section (row 4)
        tables.Add(new("L1", 4, 2, "lounge"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    // ── Default fallback layout ────────────────────────────────────────
    private static EventLayout DefaultGridLayout(Event ev, TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("premium", "Premium Table", 6, TableShape.Rectangle, rect, 10000),
            new("standard", "Standard Table", 4, TableShape.Round, round, 5000),
            new("cocktail", "Cocktail Table", 2, TableShape.Cocktail, cocktail, 3000),
        };

        var rows = ev.GridRows ?? 6;
        var cols = ev.GridCols ?? 6;
        var tables = new List<TableInstanceDef>();

        // Premium tables (row 0)
        tables.Add(new("T1", 0, 1, "premium"));
        tables.Add(new("T2", 0, cols - 2, "premium"));
        // Standard tables (middle rows)
        var midStart = 1;
        var midEnd = Math.Max(midStart + 1, rows - 2);
        var label = 3;
        for (var r = midStart; r <= midEnd; r++)
        {
            for (var c = 0; c < cols; c += 2)
            {
                tables.Add(new($"T{label++}", r, c, "standard"));
            }
        }
        // Cocktail tables (last row)
        var lastRow = rows - 1;
        tables.Add(new($"T{label++}", lastRow, 0, "cocktail"));
        tables.Add(new($"T{label++}", lastRow, cols / 2, "cocktail"));
        tables.Add(new($"T{label++}", lastRow, cols - 1, "cocktail"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    // ── Internal types ─────────────────────────────────────────────────
    private record EventTableDef(string Key, string Label, int Capacity, TableShape Shape, TableTemplate Template, int PriceCents);
    private record TableInstanceDef(string Label, int Row, int Col, string EventTableKey);
    private record EventLayout(EventTableDef[] EventTableDefs, TableInstanceDef[] TableDefs);
}
