using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds 10 real Mobile/Gulf Coast AL venues and ~10 events using Grid or Open layout modes.
/// Uses stored procedures for all writes to validate SPs on every dev startup.
/// </summary>
public static class VenueEventSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var venueProc = scope.ServiceProvider.GetRequiredService<IVenueProcedures>();
        var eventProc = scope.ServiceProvider.GetRequiredService<IEventProcedures>();

        if (await context.Venues.AnyAsync())
            return;

        var organizer = await context.Users.FirstAsync(u => u.Email == "organizer@code829.local");
        var admin = await context.Users.FirstAsync(u => u.Email == "admin@code829.local");

        var venueIds = await SeedVenuesAsync(venueProc);
        Log.Information("[Seed] Created {Count} venues via SP", venueIds.Count);

        await SeedEventsAsync(eventProc, venueIds, organizer.Id, admin.Id);
        var eventCount = await context.Events.CountAsync();
        Log.Information("[Seed] Created {Count} events via SP", eventCount);
    }

    private static async Task<List<Guid>> SeedVenuesAsync(IVenueProcedures venueProc)
    {
        var venueData = new (string Name, string Line1, string City, string State, string Zip, string? Phone, string? Email, string? Website, string Desc)[]
        {
            ("The Saenger Theatre", "6 S Joachim St", "Mobile", "AL", "36602", "(251) 208-5600", "events@saengertheatre.com", "https://mobilesaenger.com",
                "Historic 1927 theatre featuring Spanish Baroque architecture, hosting concerts, Broadway shows, and special events in downtown Mobile."),
            ("Mobile Convention Center", "1 S Water St", "Mobile", "AL", "36602", "(251) 208-2100", "info@mobileconvention.com", "https://mobilecivicctr.com",
                "Premier convention facility on the waterfront with flexible event spaces for conferences, expos, and large gatherings."),
            ("The Blind Mule", "57 N Claiborne St", "Mobile", "AL", "36602", "(251) 694-6853", null, null,
                "Intimate downtown venue and gastropub known for craft cocktails, local music, and a vibrant nightlife atmosphere."),
            ("Moe's Original BBQ", "6423 Old Shell Rd", "Mobile", "AL", "36608", "(251) 380-7427", null, "https://moesoriginalbbq.com",
                "Alabama-style BBQ joint with a laid-back patio and stage for live blues, country, and Americana acts."),
            ("The Soul Kitchen", "219 Dauphin St", "Mobile", "AL", "36602", "(251) 433-5958", null, null,
                "Eclectic Dauphin Street venue combining Southern cuisine with live music, poetry nights, and community events."),
            ("Hank Aaron Stadium Area", "755 Bolling Brothers Blvd", "Mobile", "AL", "36606", "(251) 479-2327", null, null,
                "Open-air stadium complex hosting sporting events, festivals, and large outdoor concerts in Mobile."),
            ("Bellingrath Gardens", "12401 Bellingrath Gardens Rd", "Theodore", "AL", "36582", "(251) 973-2217", "info@bellingrath.org", "https://bellingrath.org",
                "Stunning 65-acre garden estate south of Mobile offering outdoor events surrounded by azaleas, roses, and live oaks."),
            ("OWA Amusement Park", "1501 S OWA Blvd", "Foley", "AL", "36535", "(251) 923-2111", null, "https://visitowa.com",
                "Family entertainment destination on the Gulf Coast with rides, dining, and a bustling downtown district for events."),
            ("The Wharf Amphitheatre", "4830 Main St", "Orange Beach", "AL", "36561", "(251) 224-1020", null, "https://alwharf.com",
                "Premier outdoor amphitheatre on the Alabama Gulf Coast hosting national touring acts and major festivals."),
            ("Fairhope Civic Center", "161 N Section St", "Fairhope", "AL", "36532", "(251) 928-2136", "info@fairhopeal.gov", null,
                "Charming civic center in the arts community of Fairhope, hosting lectures, dances, and cultural events on Mobile Bay."),
        };

        var venueIds = new List<Guid>();
        foreach (var (name, line1, city, state, zip, phone, email, website, desc) in venueData)
        {
            var id = await venueProc.CreateVenueAsync(name, desc, null, phone, email, website, line1, null, city, state, zip);
            venueIds.Add(id);
        }

        return venueIds;
    }

    private static async Task SeedEventsAsync(IEventProcedures eventProc, List<Guid> venueIds, Guid organizerId, Guid adminId)
    {
        var now = DateTime.UtcNow;

        // Grid events — price per table, tables created by LayoutSeeder
        var gridEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, int Rows, int Cols)[]
        {
            ("Bellingrath Gardens Spring Gala", "Elegant evening fundraiser among the azaleas. Live orchestra, gourmet dinner, and silent auction.",
                EventCategory.Social, EventStatus.Published, 6, adminId, 5, true, 6, 8),
            ("Farm-to-Table Dinner: Spring Harvest", "Multi-course dinner featuring local farms and Gulf seafood, served under the oaks at Bellingrath.",
                EventCategory.Dining, EventStatus.Published, 6, adminId, 3, false, 4, 6),
            ("Mobile Business Leaders Luncheon", "Quarterly networking luncheon for Mobile's business community. Keynote speaker from the chamber of commerce.",
                EventCategory.Business, EventStatus.Published, 1, adminId, 3, false, 5, 8),
            ("Blind Mule Comedy Night", "Stand-up comedy showcase featuring rising Southern comedians. Two-drink minimum. Ages 21+.",
                EventCategory.Social, EventStatus.Published, 2, organizerId, 2, false, 4, 5),
            ("Fairhope Wine & Dine Gala", "An exquisite evening of wine pairings and local cuisine celebrating Fairhope's culinary scene.",
                EventCategory.Dining, EventStatus.Published, 9, adminId, 4, true, 5, 6),
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, rows, cols) in gridEvents)
        {
            var startDate = now.AddDays(weeksOut * 7).Date.AddHours(18);
            await eventProc.CreateEventAsync(
                title, GenerateSlug(title), desc, status.ToString(), cat.ToString(),
                DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                DateTime.SpecifyKind(startDate.AddHours(4), DateTimeKind.Utc),
                null, featured, LayoutMode.Grid.ToString(), null, null, null, null,
                rows, cols, venueIds[venueIdx], orgId, null);
        }

        // Open events — price per person with MaxCapacity
        var openEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, int PricePerPersonCents, int MaxCapacity)[]
        {
            ("Gulf Coast Jazz Night", "An evening of smooth jazz featuring Gulf Coast musicians. Enjoy craft cocktails and Southern appetizers under the stars.",
                EventCategory.Music, EventStatus.Published, 0, organizerId, 2, true, 3500, 800),
            ("Gulf Shores Summer Kickoff Concert", "National headliner TBA. The biggest outdoor concert of the summer on the Alabama Gulf Coast.",
                EventCategory.Music, EventStatus.Published, 8, organizerId, 6, true, 4500, 5000),
            ("Downtown Blues & BBQ Fest", "Live blues music paired with the best BBQ the Gulf Coast has to offer. Family-friendly outdoor festival.",
                EventCategory.Music, EventStatus.Published, 3, organizerId, 2, false, 2500, 300),
            ("Kids' Adventure Day at OWA", "A day of rides, face painting, magic shows, and family fun at OWA Amusement Park.",
                EventCategory.Family, EventStatus.Published, 7, organizerId, 4, false, 2500, 1200),
            ("5K Run for the Bay", "Scenic 5K along Mobile Bay supporting coastal conservation. Post-race party with live music.",
                EventCategory.Sports, EventStatus.Published, 5, organizerId, 6, false, 3500, 2000),
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, pricePerPerson, maxCap) in openEvents)
        {
            var startDate = now.AddDays(weeksOut * 7).Date.AddHours(18);
            await eventProc.CreateEventAsync(
                title, GenerateSlug(title), desc, status.ToString(), cat.ToString(),
                DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                DateTime.SpecifyKind(startDate.AddHours(4), DateTimeKind.Utc),
                null, featured, LayoutMode.Open.ToString(), maxCap, pricePerPerson, null, null,
                null, null, venueIds[venueIdx], orgId, null);
        }

        // Draft events (1 Grid, 1 Open)
        var draftGridStart = DateTime.SpecifyKind(now.AddDays(49).Date.AddHours(19), DateTimeKind.Utc);
        await eventProc.CreateEventAsync(
            "Mobile Mardi Gras Preview Ball", GenerateSlug("Mobile Mardi Gras Preview Ball"),
            "Exclusive preview of the upcoming Mardi Gras season with floats, royalty, and brass bands.",
            EventStatus.Draft.ToString(), EventCategory.Social.ToString(),
            draftGridStart, draftGridStart.AddHours(4),
            null, false, LayoutMode.Grid.ToString(), null, null, null, null,
            6, 8, venueIds[1], adminId, null);

        var draftOpenStart = DateTime.SpecifyKind(now.AddDays(56).Date.AddHours(9), DateTimeKind.Utc);
        await eventProc.CreateEventAsync(
            "Summer Coding Bootcamp for Kids", GenerateSlug("Summer Coding Bootcamp for Kids"),
            "Two-week intensive coding program for ages 10-16. Learn Python, web development, and game design.",
            EventStatus.Draft.ToString(), EventCategory.Tech.ToString(),
            draftOpenStart, draftOpenStart.AddHours(7),
            null, false, LayoutMode.Open.ToString(), 30, 29900, null, null,
            null, null, venueIds[9], organizerId, null);
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }
}
