using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds EventTables and Table instances for Grid events via stored procedures.
/// 3-tier model: TableTemplate → EventTable → Table.
/// Runs after VenueEventSeeder and DataSeeder.
/// </summary>
public static class LayoutSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var tableProc = scope.ServiceProvider.GetRequiredService<ITableProcedures>();

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
            await SeedEventTablesAndInstancesAsync(tableProc, ev, roundTemplate, rectTemplate, cocktailTemplate, squareTemplate);
        }

        Log.Information("[Seed] Created event tables and table instances for {Count} grid events via SP", gridEvents.Count);
    }

    private static async Task SeedEventTablesAndInstancesAsync(
        ITableProcedures tableProc, Event ev,
        TableTemplate round, TableTemplate rect, TableTemplate cocktail, TableTemplate square)
    {
        var layout = GetEventLayout(ev, round, rect, cocktail, square);

        // Create EventTable records via SP
        var eventTableMap = new Dictionary<string, Guid>();
        foreach (var etDef in layout.EventTableDefs)
        {
            var etId = await tableProc.CreateEventTableAsync(
                ev.Id, etDef.Label, etDef.Capacity, etDef.Shape.ToString(),
                etDef.Template.DefaultColor, etDef.PriceCents, null, etDef.Template.Id);
            eventTableMap[etDef.Key] = etId;
        }

        // Create Table instances via SP
        var sortOrder = 0;
        foreach (var tableDef in layout.TableDefs)
        {
            var eventTableId = eventTableMap[tableDef.EventTableKey];
            await tableProc.CreateTableAsync(eventTableId, ev.Id, tableDef.Label, tableDef.Row, tableDef.Col, sortOrder++);
        }
    }

    private static EventLayout GetEventLayout(
        Event ev, TableTemplate round, TableTemplate rect, TableTemplate cocktail, TableTemplate square)
    {
        return ev.Title switch
        {
            var t when t.Contains("Gala") && t.Contains("Bellingrath") => BellingrathGalaLayout(round, rect, cocktail),
            var t when t.Contains("Farm-to-Table") => FarmToTableLayout(round, rect, cocktail),
            var t when t.Contains("Luncheon") => LuncheonLayout(round, rect),
            var t when t.Contains("Comedy") => ComedyNightLayout(round, rect, cocktail),
            var t when t.Contains("Wine & Dine") => WineDineLayout(round, rect, square),
            _ => DefaultGridLayout(ev, round, rect, cocktail),
        };
    }

    private static EventLayout BellingrathGalaLayout(TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("vip", "VIP Table", 6, TableShape.Rectangle, rect, 15000),
            new("standard", "Standard Table", 4, TableShape.Round, round, 7500),
            new("back", "Back Row Table", 4, TableShape.Round, round, 5000),
            new("cocktail", "Cocktail High-Top", 2, TableShape.Cocktail, cocktail, 3000),
        };

        var tables = new List<TableInstanceDef>
        {
            new("A1", 0, 1, "vip"), new("A2", 0, 3, "vip"), new("A3", 0, 5, "vip"),
            new("B1", 2, 0, "standard"), new("B2", 2, 2, "standard"), new("B3", 2, 4, "standard"), new("B4", 2, 6, "standard"),
            new("B5", 3, 1, "standard"), new("B6", 3, 3, "standard"), new("B7", 3, 5, "standard"), new("B8", 3, 7, "standard"),
            new("C1", 4, 0, "back"), new("C2", 4, 2, "back"), new("C3", 4, 4, "back"), new("C4", 4, 6, "back"),
            new("D1", 5, 1, "cocktail"), new("D2", 5, 3, "cocktail"), new("D3", 5, 5, "cocktail"), new("D4", 5, 7, "cocktail"),
        };

        return new EventLayout(eventTables, tables.ToArray());
    }

    private static EventLayout FarmToTableLayout(TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("chef", "Chef's Table", 6, TableShape.Rectangle, rect, 12000),
            new("garden", "Garden Table", 4, TableShape.Round, round, 8500),
            new("cocktail", "Herb Garden High-Top", 2, TableShape.Cocktail, cocktail, 4500),
        };

        var tables = new List<TableInstanceDef>
        {
            new("Chef1", 0, 2, "chef"),
            new("G1", 1, 0, "garden"), new("G2", 1, 2, "garden"), new("G3", 1, 4, "garden"),
            new("P1", 2, 1, "garden"), new("P2", 2, 3, "garden"), new("P3", 2, 5, "garden"),
            new("H1", 3, 0, "cocktail"), new("H2", 3, 2, "cocktail"), new("H3", 3, 4, "cocktail"),
        };

        return new EventLayout(eventTables, tables.ToArray());
    }

    private static EventLayout LuncheonLayout(TableTemplate round, TableTemplate rect)
    {
        var eventTables = new EventTableDef[]
        {
            new("head", "Head Table", 8, TableShape.Rectangle, rect, 10000),
            new("standard", "Standard Table", 4, TableShape.Round, round, 3500),
        };

        var tables = new List<TableInstanceDef>
        {
            new("Head", 0, 3, "head"),
            new("S1", 1, 0, "standard"), new("S2", 1, 2, "standard"), new("S3", 1, 4, "standard"), new("S4", 1, 6, "standard"),
            new("S5", 2, 1, "standard"), new("S6", 2, 3, "standard"), new("S7", 2, 5, "standard"), new("S8", 2, 7, "standard"),
            new("S9", 3, 0, "standard"), new("S10", 3, 3, "standard"), new("S11", 3, 6, "standard"),
        };

        return new EventLayout(eventTables, tables.ToArray());
    }

    private static EventLayout ComedyNightLayout(TableTemplate round, TableTemplate rect, TableTemplate cocktail)
    {
        var eventTables = new EventTableDef[]
        {
            new("front", "Front Row Table", 4, TableShape.Rectangle, rect, 5000),
            new("middle", "Middle Table", 4, TableShape.Round, round, 3000),
            new("back", "Bar High-Top", 2, TableShape.Cocktail, cocktail, 1500),
        };

        var tables = new List<TableInstanceDef>
        {
            new("F1", 0, 1, "front"), new("F2", 0, 2, "front"), new("F3", 0, 3, "front"),
            new("M1", 1, 0, "middle"), new("M2", 1, 2, "middle"), new("M3", 1, 4, "middle"),
            new("M4", 2, 1, "middle"), new("M5", 2, 3, "middle"),
            new("B1", 3, 0, "back"), new("B2", 3, 1, "back"), new("B3", 3, 3, "back"), new("B4", 3, 4, "back"),
        };

        return new EventLayout(eventTables, tables.ToArray());
    }

    private static EventLayout WineDineLayout(TableTemplate round, TableTemplate rect, TableTemplate square)
    {
        var eventTables = new EventTableDef[]
        {
            new("vip", "VIP Wine Table", 6, TableShape.Rectangle, rect, 12000),
            new("standard", "Dining Table", 4, TableShape.Round, round, 8000),
            new("value", "Garden Table", 4, TableShape.Round, round, 6000),
            new("lounge", "Lounge Section", 8, TableShape.Square, square, 15000),
        };

        var tables = new List<TableInstanceDef>
        {
            new("V1", 0, 1, "vip"), new("V2", 0, 4, "vip"),
            new("R1", 1, 0, "standard"), new("R2", 1, 2, "standard"), new("R3", 1, 4, "standard"), new("R4", 2, 1, "standard"),
            new("R5", 3, 0, "value"), new("R6", 3, 2, "value"), new("R7", 3, 4, "value"),
            new("L1", 4, 2, "lounge"),
        };

        return new EventLayout(eventTables, tables.ToArray());
    }

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
        var tables = new List<TableInstanceDef>
        {
            new("T1", 0, 1, "premium"),
            new("T2", 0, cols - 2, "premium"),
        };

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

        var lastRow = rows - 1;
        tables.Add(new($"T{label++}", lastRow, 0, "cocktail"));
        tables.Add(new($"T{label++}", lastRow, cols / 2, "cocktail"));
        tables.Add(new($"T{label++}", lastRow, cols - 1, "cocktail"));

        return new EventLayout(eventTables, tables.ToArray());
    }

    private record EventTableDef(string Key, string Label, int Capacity, TableShape Shape, TableTemplate Template, int PriceCents);
    private record TableInstanceDef(string Label, int Row, int Col, string EventTableKey);
    private record EventLayout(EventTableDef[] EventTableDefs, TableInstanceDef[] TableDefs);
}
