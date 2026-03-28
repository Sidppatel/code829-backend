using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds layout tables and pricing rules for existing events.
/// Runs after VenueEventSeeder. Assigns layout modes, places tables,
/// and creates pricing rules for the 18 seeded events.
/// </summary>
public static class LayoutPricingSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();

        // Skip if pricing rules already exist
        if (await context.PricingRules.AnyAsync(r => r.Name != null))
            return;

        var events = await context.Events
            .Include(e => e.Venue)
            .OrderBy(e => e.StartDate)
            .ToListAsync();

        if (events.Count == 0) return;

        var tableTypes = await context.TableTypes.Where(tt => tt.VenueId == null).ToListAsync();
        if (tableTypes.Count == 0) return;

        var roundType = tableTypes.First(t => t.DefaultShape == TableShape.Round);
        var rectType = tableTypes.First(t => t.DefaultShape == TableShape.Rectangle);
        var cocktailType = tableTypes.First(t => t.DefaultShape == TableShape.Cocktail);
        var squareType = tableTypes.First(t => t.DefaultShape == TableShape.Square);

        var idx = 0;
        foreach (var ev in events)
        {
            // Assign layout modes in a pattern: first 6 = Grid, next 6 = CapacityOnly, rest = None
            if (idx < 6)
            {
                ev.LayoutMode = LayoutMode.Grid;
                ev.EditorMode = EditorMode.Grid;
                ev.GridRows = 5 + (idx % 3);
                ev.GridCols = 5 + (idx % 4);
                SeedGridTables(context, ev, roundType, rectType, cocktailType, idx);
            }
            else if (idx < 12)
            {
                ev.LayoutMode = LayoutMode.CapacityOnly;
                ev.MaxCapacity = new[] { 100, 200, 500, 1500, 2000, 5000 }[idx - 6];
            }
            else
            {
                ev.LayoutMode = LayoutMode.None;
            }

            SeedPricingRules(context, ev, idx);
            idx++;
        }

        await context.SaveChangesAsync();
        Log.Information("[Seed] Configured layouts and pricing for {Count} events", events.Count);
    }

    private static void SeedGridTables(
        EventPlatformDbContext context, Event ev,
        TableType round, TableType rect, TableType cocktail, int eventIdx)
    {
        var tableCount = 8 + (eventIdx * 2);
        var sections = new[] { "VIP", "Standard", "Premium" };

        for (var i = 0; i < tableCount && i < (ev.GridRows ?? 5) * (ev.GridCols ?? 5); i++)
        {
            var row = i / (ev.GridCols ?? 5);
            var col = i % (ev.GridCols ?? 5);
            var section = sections[i % sections.Length];
            var isVip = section == "VIP";
            var tableType = isVip ? rect : (i % 3 == 2 ? cocktail : round);
            var colLetter = (char)('A' + col);

            context.Tables.Add(new Table
            {
                Id = Guid.NewGuid(),
                Label = $"{colLetter}{row + 1}",
                Capacity = tableType.DefaultCapacity,
                Shape = tableType.DefaultShape,
                Color = isVip ? "#7c3aed" : (section == "Premium" ? "#f97316" : "#4f46e5"),
                Section = section,
                PriceType = PriceType.PerSeat,
                PriceCents = isVip ? 7500 : (section == "Premium" ? 5000 : 3500),
                IsActive = true,
                GridRow = row,
                GridCol = col,
                Width = 80,
                Height = 80,
                SortOrder = i,
                TableTypeId = tableType.Id,
                EventId = ev.Id,
                VenueId = ev.VenueId
            });
        }
    }

    private static void SeedPricingRules(EventPlatformDbContext context, Event ev, int idx)
    {
        // Standard rule for all events
        context.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            Name = "Standard",
            Type = PricingRuleType.Standard,
            PriceCents = 2500 + (idx * 500),
            IsActive = true,
            SortOrder = 0
        });

        // EarlyBird for even-indexed events
        if (idx % 2 == 0)
        {
            context.PricingRules.Add(new PricingRule
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Name = "Early Bird",
                Type = PricingRuleType.EarlyBird,
                PriceCents = 1500 + (idx * 300),
                ValidFrom = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(14),
                IsActive = true,
                SortOrder = 1
            });
        }

        // FirstN for every 3rd event
        if (idx % 3 == 0)
        {
            context.PricingRules.Add(new PricingRule
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Name = "First 50 Tickets",
                Type = PricingRuleType.FirstN,
                PriceCents = 1000 + (idx * 200),
                MaxCount = 50,
                UsedCount = idx * 3,
                IsActive = true,
                SortOrder = 2
            });
        }
    }
}
