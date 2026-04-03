using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds tables for Grid events. Each table has a position (PosX/PosY as percentages 0-100),
/// capacity, shape, color, and price. Runs after VenueEventSeeder.
/// </summary>
public static class LayoutSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();

        if (await context.Tables.AnyAsync())
            return;

        var gridEvents = await context.Events
            .Where(e => e.LayoutMode == LayoutMode.Grid && e.Status == EventStatus.Published)
            .ToListAsync();

        if (gridEvents.Count == 0) return;

        var tableTypes = await context.TableTypes.ToListAsync();
        if (tableTypes.Count == 0) return;

        var roundType = tableTypes.First(t => t.DefaultShape == TableShape.Round);
        var rectType = tableTypes.First(t => t.DefaultShape == TableShape.Rectangle);
        var cocktailType = tableTypes.First(t => t.DefaultShape == TableShape.Cocktail);
        var squareType = tableTypes.First(t => t.DefaultShape == TableShape.Square);

        foreach (var ev in gridEvents)
        {
            SeedTablesForEvent(context, ev, roundType, rectType, cocktailType, squareType);
        }

        await context.SaveChangesAsync();
        Log.Information("[Seed] Created tables for {Count} grid events", gridEvents.Count);
    }

    private static void SeedTablesForEvent(
        EventPlatformDbContext context, Event ev,
        TableType round, TableType rect, TableType cocktail, TableType square)
    {
        var tables = ev.Title switch
        {
            var t when t.Contains("Gala") && t.Contains("Bellingrath") => new TableDef[]
            {
                // VIP front row
                new("A1", 6, TableShape.Rectangle, rect, 15000, 15, 10),
                new("A2", 6, TableShape.Rectangle, rect, 15000, 40, 10),
                new("A3", 6, TableShape.Rectangle, rect, 12000, 65, 10),
                // Standard middle rows
                new("B1", 4, TableShape.Round, round, 7500, 10, 30),
                new("B2", 4, TableShape.Round, round, 7500, 30, 30),
                new("B3", 4, TableShape.Round, round, 7500, 50, 30),
                new("B4", 4, TableShape.Round, round, 7500, 70, 30),
                new("B5", 4, TableShape.Round, round, 7500, 90, 30),
                // Back rows
                new("C1", 4, TableShape.Round, round, 5000, 15, 55),
                new("C2", 4, TableShape.Round, round, 5000, 40, 55),
                new("C3", 4, TableShape.Round, round, 5000, 65, 55),
                new("C4", 4, TableShape.Round, round, 5000, 90, 55),
                // Cocktail high-tops along the sides
                new("D1", 2, TableShape.Cocktail, cocktail, 3000, 5, 75),
                new("D2", 2, TableShape.Cocktail, cocktail, 3000, 25, 75),
                new("D3", 2, TableShape.Cocktail, cocktail, 3000, 50, 75),
                new("D4", 2, TableShape.Cocktail, cocktail, 3000, 75, 75),
            },
            var t when t.Contains("Farm-to-Table") => new TableDef[]
            {
                new("Chef1", 6, TableShape.Rectangle, rect, 12000, 50, 10),
                new("G1", 4, TableShape.Round, round, 8500, 15, 35),
                new("G2", 4, TableShape.Round, round, 8500, 40, 35),
                new("G3", 4, TableShape.Round, round, 8500, 65, 35),
                new("G4", 4, TableShape.Round, round, 8500, 90, 35),
                new("P1", 4, TableShape.Round, round, 8500, 25, 60),
                new("P2", 4, TableShape.Round, round, 8500, 55, 60),
                new("P3", 4, TableShape.Round, round, 8500, 80, 60),
                new("H1", 2, TableShape.Cocktail, cocktail, 4500, 10, 85),
                new("H2", 2, TableShape.Cocktail, cocktail, 4500, 35, 85),
                new("H3", 2, TableShape.Cocktail, cocktail, 4500, 60, 85),
                new("H4", 2, TableShape.Cocktail, cocktail, 4500, 85, 85),
            },
            var t when t.Contains("Luncheon") => new TableDef[]
            {
                new("Head", 8, TableShape.Rectangle, rect, 10000, 50, 8),
                new("S1", 4, TableShape.Round, round, 3500, 12, 30),
                new("S2", 4, TableShape.Round, round, 3500, 35, 30),
                new("S3", 4, TableShape.Round, round, 3500, 58, 30),
                new("S4", 4, TableShape.Round, round, 3500, 82, 30),
                new("S5", 4, TableShape.Round, round, 3500, 12, 55),
                new("S6", 4, TableShape.Round, round, 3500, 35, 55),
                new("S7", 4, TableShape.Round, round, 3500, 58, 55),
                new("S8", 4, TableShape.Round, round, 3500, 82, 55),
                new("S9", 4, TableShape.Round, round, 3500, 25, 80),
                new("S10", 4, TableShape.Round, round, 3500, 50, 80),
                new("S11", 4, TableShape.Round, round, 3500, 75, 80),
            },
            var t when t.Contains("Comedy") => new TableDef[]
            {
                new("F1", 4, TableShape.Rectangle, rect, 5000, 20, 12),
                new("F2", 4, TableShape.Rectangle, rect, 5000, 50, 12),
                new("F3", 4, TableShape.Rectangle, rect, 5000, 80, 12),
                new("M1", 4, TableShape.Round, round, 3000, 12, 38),
                new("M2", 4, TableShape.Round, round, 3000, 38, 38),
                new("M3", 4, TableShape.Round, round, 3000, 62, 38),
                new("M4", 4, TableShape.Round, round, 3000, 88, 38),
                new("B1", 2, TableShape.Cocktail, cocktail, 1500, 15, 65),
                new("B2", 2, TableShape.Cocktail, cocktail, 1500, 40, 65),
                new("B3", 2, TableShape.Cocktail, cocktail, 1500, 65, 65),
                new("B4", 2, TableShape.Cocktail, cocktail, 1500, 88, 65),
            },
            var t when t.Contains("Wine & Dine") => new TableDef[]
            {
                new("V1", 6, TableShape.Rectangle, rect, 12000, 25, 10),
                new("V2", 6, TableShape.Rectangle, rect, 12000, 75, 10),
                new("R1", 4, TableShape.Round, round, 8000, 15, 35),
                new("R2", 4, TableShape.Round, round, 8000, 40, 35),
                new("R3", 4, TableShape.Round, round, 8000, 65, 35),
                new("R4", 4, TableShape.Round, round, 8000, 90, 35),
                new("R5", 4, TableShape.Round, round, 6000, 25, 60),
                new("R6", 4, TableShape.Round, round, 6000, 55, 60),
                new("R7", 4, TableShape.Round, round, 6000, 85, 60),
                new("L1", 8, TableShape.Square, square, 15000, 50, 85),
            },
            _ => new TableDef[]
            {
                new("T1", 6, TableShape.Rectangle, rect, 10000, 25, 15),
                new("T2", 6, TableShape.Rectangle, rect, 10000, 75, 15),
                new("T3", 4, TableShape.Round, round, 5000, 20, 45),
                new("T4", 4, TableShape.Round, round, 5000, 50, 45),
                new("T5", 4, TableShape.Round, round, 5000, 80, 45),
                new("T6", 2, TableShape.Cocktail, cocktail, 3000, 15, 75),
                new("T7", 2, TableShape.Cocktail, cocktail, 3000, 50, 75),
                new("T8", 2, TableShape.Cocktail, cocktail, 3000, 85, 75),
            },
        };

        for (var i = 0; i < tables.Length; i++)
        {
            var t = tables[i];
            context.Tables.Add(new Table
            {
                Id = Guid.NewGuid(),
                Label = t.Label,
                Capacity = t.Capacity,
                Shape = t.Shape,
                Color = t.Type.DefaultColor,
                PriceCents = t.PriceCents,
                PosX = t.PosX,
                PosY = t.PosY,
                SortOrder = i,
                IsActive = true,
                TableTypeId = t.Type.Id,
                EventId = ev.Id,
                VenueId = ev.VenueId
            });
        }
    }

    private record TableDef(string Label, int Capacity, TableShape Shape, TableType Type, int PriceCents, double PosX, double PosY);
}
