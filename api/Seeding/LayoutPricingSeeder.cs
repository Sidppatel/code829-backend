using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds tables + seats for Grid events, and pricing rules for all events.
/// Runs after VenueEventSeeder. Respects each event's existing LayoutMode —
/// only Grid events get tables and seats.
/// </summary>
public static class LayoutPricingSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();

        if (await context.PricingRules.AnyAsync(r => r.Name != null))
            return;

        var events = await context.Events
            .Include(e => e.TicketTypes)
            .OrderBy(e => e.StartDate)
            .ToListAsync();

        if (events.Count == 0) return;

        var tableTypes = await context.TableTypes.Where(tt => tt.VenueId == null).ToListAsync();
        if (tableTypes.Count == 0) return;

        var roundType = tableTypes.First(t => t.DefaultShape == TableShape.Round);
        var rectType = tableTypes.First(t => t.DefaultShape == TableShape.Rectangle);
        var cocktailType = tableTypes.First(t => t.DefaultShape == TableShape.Cocktail);

        foreach (var ev in events)
        {
            if (ev.LayoutMode == LayoutMode.Grid)
            {
                SeedGridTables(context, ev, roundType, rectType, cocktailType);
                SeedSeatsForTables(context, ev.Id);
            }

            SeedPricingRules(context, ev);
        }

        await context.SaveChangesAsync();
        Log.Information("[Seed] Configured layouts and pricing for {Count} events", events.Count);
    }

    /// <summary>
    /// Creates tables on a grid for assigned-seating events.
    /// Uses PerTable pricing — the table price is for the whole table.
    /// </summary>
    private static void SeedGridTables(
        EventPlatformDbContext context, Event ev,
        TableType round, TableType rect, TableType cocktail)
    {
        // Grid dimensions based on event — set here if not already set
        var (rows, cols, tableConfigs) = ev.Title switch
        {
            var t when t.Contains("Gala") => (6, 8, new TableConfig[]
            {
                new("VIP", rect, 15000, 6),
                new("VIP", rect, 15000, 4),
                new("Standard", round, 7500, 12),
                new("Standard", round, 7500, 10),
                new("Premium", cocktail, 5000, 4),
            }),
            var t when t.Contains("Farm-to-Table") => (4, 6, new TableConfig[]
            {
                new("Chef's Table", rect, 12000, 2),
                new("Garden", round, 8500, 6),
                new("Patio", round, 8500, 4),
                new("Intimate", cocktail, 4500, 3),
            }),
            var t when t.Contains("Luncheon") => (5, 8, new TableConfig[]
            {
                new("Head Table", rect, 10000, 2),
                new("Standard", round, 3500, 14),
                new("Standard", round, 3500, 6),
            }),
            var t when t.Contains("Comedy") => (4, 5, new TableConfig[]
            {
                new("Front Row", rect, 5000, 3),
                new("Standard", round, 2000, 8),
                new("Bar Side", cocktail, 1500, 4),
            }),
            _ => (5, 6, new TableConfig[]
            {
                new("VIP", rect, 10000, 4),
                new("Standard", round, 5000, 8),
            }),
        };

        ev.GridRows = rows;
        ev.GridCols = cols;

        var tableIdx = 0;
        foreach (var config in tableConfigs)
        {
            for (var i = 0; i < config.Count; i++)
            {
                var gridPos = tableIdx;
                if (gridPos >= rows * cols) break;

                var row = gridPos / cols;
                var col = gridPos % cols;
                var colLetter = (char)('A' + col);

                context.Tables.Add(new Table
                {
                    Id = Guid.NewGuid(),
                    Label = $"{colLetter}{row + 1}",
                    Capacity = config.TableType.DefaultCapacity,
                    Shape = config.TableType.DefaultShape,
                    Color = config.TableType.DefaultColor,
                    Section = config.Section,
                    PriceType = PriceType.PerTable,
                    PriceCents = config.PriceCents,
                    IsActive = true,
                    GridRow = row,
                    GridCol = col,
                    SortOrder = tableIdx,
                    TableTypeId = config.TableType.Id,
                    EventId = ev.Id,
                    VenueId = ev.VenueId
                });
                tableIdx++;
            }
        }
    }

    private static void SeedSeatsForTables(EventPlatformDbContext context, Guid eventId)
    {
        var tables = context.Tables.Local.Where(t => t.EventId == eventId).ToList();
        foreach (var table in tables)
        {
            var capacity = table.Capacity ?? 0;
            for (var i = 1; i <= capacity; i++)
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
    }

    private static void SeedPricingRules(EventPlatformDbContext context, Event ev)
    {
        // Standard pricing rule for every event
        var basePrice = ev.TicketTypes.FirstOrDefault()?.PriceCents ?? 2500;
        context.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            Name = "Standard",
            Type = PricingRuleType.Standard,
            PriceCents = basePrice,
            IsActive = true,
            SortOrder = 0
        });
    }

    private record TableConfig(string Section, TableType TableType, int PriceCents, int Count);
}
