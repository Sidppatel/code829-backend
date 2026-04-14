using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds EventTicketType records for Open layout events via stored procedures.
/// Runs after VenueEventSeeder and before BookingSeeder.
/// </summary>
public static class TicketTypeSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var ticketTypeProc = scope.ServiceProvider.GetRequiredService<IEventTicketTypeProcedures>();

        if (await context.EventTicketTypes.AnyAsync())
            return;

        var openEvents = await context.Events
            .Where(e => e.LayoutMode == LayoutMode.Open && e.Status == EventStatus.Published)
            .ToListAsync();

        if (openEvents.Count == 0) return;

        foreach (var ev in openEvents)
        {
            var tiers = GetTiersForEvent(ev.Title, ev.PricePerPersonCents ?? 2500);

            var sortOrder = 0;
            foreach (var (label, priceCents, maxQty) in tiers)
            {
                await ticketTypeProc.CreateAsync(ev.Id, label, priceCents, null, maxQty, sortOrder++, null);
            }
        }

        var total = await context.EventTicketTypes.CountAsync();
        Log.Information("[Seed] Created {Total} event ticket types via SP", total);
    }

    private static List<(string Label, int PriceCents, int? MaxQuantity)> GetTiersForEvent(string title, int basePriceCents)
    {
        return title switch
        {
            var t when t.Contains("Jazz") => [
                ("VIP", 7500, 100),
                ("General Admission", 3500, null),
                ("Student", 2000, 150)
            ],
            var t when t.Contains("Kickoff Concert") => [
                ("VIP", 9500, 500),
                ("General Admission", 4500, null),
                ("Early Bird", 3500, 1000)
            ],
            var t when t.Contains("Blues") => [
                ("Pit Pass", 5000, 50),
                ("General Admission", 2500, null)
            ],
            var t when t.Contains("Kids") || t.Contains("Adventure") => [
                ("Family Pack (4)", 8000, null),
                ("Individual", 2500, null)
            ],
            _ => [
                ("General Admission", basePriceCents, null)
            ]
        };
    }
}
