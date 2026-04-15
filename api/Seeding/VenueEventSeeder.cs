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
        var ticketTypeProc = scope.ServiceProvider.GetRequiredService<IEventTicketTypeProcedures>();

        if (await context.Venues.AnyAsync())
            return;

        var organizer = await context.AdminUsers.FirstAsync(u => u.Email == "organizer@code829.local");
        var admin = await context.AdminUsers.FirstAsync(u => u.Email == "admin@code829.local");

        var venueIds = await SeedVenuesAsync(venueProc);
        Log.Information("[Seed] Created {Count} venues via SP", venueIds.Count);

        await SeedEventsAsync(eventProc, ticketTypeProc, venueIds, organizer.Id, admin.Id);
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

    private static async Task SeedEventsAsync(IEventProcedures eventProc, IEventTicketTypeProcedures ticketTypeProc, List<Guid> venueIds, Guid organizerId, Guid adminId)
    {
        var now = DateTime.UtcNow;

        // Grid events
        var gridEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, int Rows, int Cols)[]
        {
            ("Bellingrath Gardens Spring Gala", "An elegant evening under the stars in the Southern estate garden. Features a five-course gourmet dinner, live jazz by the reservoir, and an exclusive charity auction of rare azalea varieties.",
                EventCategory.Social, EventStatus.Published, 6, adminId, 5, true, 6, 8),
            ("Farm-to-Table Dinner: Coastal Harvest", "Experience the finest of Mobile Bay's bounty. This seasonal harvest dinner features local heirloom vegetables, freshly caught Gulf red snapper, and pairings from regional craft breweries.",
                EventCategory.Dining, EventStatus.Published, 6, adminId, 3, false, 4, 6),
            ("Mobile Tech Leadership Summit", "Join the brightest minds in the Gulf Coast tech scene for a full day of workshops, panels, and networking focused on the future of software engineering and digital transformation in the South.",
                EventCategory.Business, EventStatus.Published, 1, adminId, 3, false, 4, 10),
            ("Blind Mule Comedy Showcase", "Laughter, libations, and live entertainment. Our monthly comedy night brings together local favorites and national touring acts for an unforgettable night in downtown Mobile.",
                EventCategory.Social, EventStatus.Published, 2, organizerId, 2, false, 4, 5),
            ("Fairhope Lakeside Jazz Festival", "Smooth saxophones and cool breezes. Fairhope's annual lakeside jazz event celebrates the rich musical heritage of the Eastern Shore with multiple stages and local food vendors.",
                EventCategory.Music, EventStatus.Published, 9, adminId, 4, true, 5, 8),
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

        // Open events
        var openEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, List<(string Label, int PriceCents, int? PlatformFeeCents, int? MaxQty, string? Desc)> Tiers)[]
        {
            ("Sunset Rhythm & Blues Festival", "A high-energy outdoor music festival featuring legendary blues artists and rising Southern soul stars. Food trucks, local artisans, and sunset views over the water.",
                EventCategory.Music, EventStatus.Published, 8, organizerId, 6, true,
                [
                    ("VIP Lounge", 12500, 2000, 100, "Includes front-of-stage access, private bar, and 2 complimentary drink tokens."),
                    ("Premium Reserved", 7500, 1500, 250, "Fixed seating in the first 10 rows with dedicated entry."),
                    ("General Admission", 3500, 1000, null, "Outdoor lawn seating. Bring your own blanket or low chair.")
                ]),
            ("Gulf Coast Coding Bootcamp", "Intensive two-week coding program designed to launch your career in tech. Learn full-stack development using modern frameworks and participate in a final capstone showcase.",
                EventCategory.Tech, EventStatus.Published, 1, organizerId, 8, false,
                [
                    ("Early Bird Professional", 85000, 5000, 20, "Early discounted rate for professionals and career changers."),
                    ("Standard Registration", 120000, 7500, 30, "Standard two-week bootcamp tuition including all materials."),
                    ("Student Scholarship Rate", 45000, 2500, 5, "Highly discounted rate for currently enrolled university students.")
                ]),
            ("Mobile Arts & Crafts Fair", "Celebrating over 100 local artisans and creators. Walk through a vibrant marketplace of handmade pottery, jewelry, paintings, and textiles in the heart of Fairhope.",
                EventCategory.Social, EventStatus.Published, 9, organizerId, 2, false,
                [
                    ("Weekend Pass", 1500, 500, null, "Full access to both days of the fair plus a commemorative tote bag."),
                    ("Single Day Entry", 1000, 300, null, "Standard admission for one day.")
                ]),
            ("Mardi Gras Coronation Ball", "The most prestigious event of the Carnival season. Witness the crowning of the 2026 King and Queen followed by an evening of orchestral music and ballroom dancing.",
                EventCategory.Social, EventStatus.Published, 0, organizerId, 4, true,
                [
                    ("Royal Tier (Front)", 25000, 5000, 50, "Front row seating and invitation to the private after-party."),
                    ("Inner Circle", 15000, 3000, 150, "Premium seating within the coronation circle."),
                    ("General Gallery", 5000, 1000, 500, "Reserved seating in the elevated gallery section.")
                ]),
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, tiers) in openEvents)
        {
            var startDate = now.AddDays(weeksOut * 7).Date.AddHours(18);
            var eventId = await eventProc.CreateEventAsync(
                title, GenerateSlug(title), desc, status.ToString(), cat.ToString(),
                DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                DateTime.SpecifyKind(startDate.AddHours(4), DateTimeKind.Utc),
                null, featured, LayoutMode.Open.ToString(), null, null, null, null,
                null, null, venueIds[venueIdx], orgId, null);

            var sortOrder = 0;
            foreach (var (label, price, platformFee, maxQty, tDesc) in tiers)
            {
                await ticketTypeProc.CreateAsync(eventId, label, price, platformFee, maxQty, sortOrder++, tDesc);
            }
        }

        // Draft events
        var draftOpenStart = DateTime.SpecifyKind(now.AddDays(56).Date.AddHours(9), DateTimeKind.Utc);
        var draftEventId = await eventProc.CreateEventAsync(
            "Summer Coding Bootcamp for Kids", GenerateSlug("Summer Coding Bootcamp for Kids"),
            "Two-week intensive coding program for ages 10-16. Learn Python, web development, and game design.",
            EventStatus.Draft.ToString(), EventCategory.Tech.ToString(),
            draftOpenStart, draftOpenStart.AddHours(7),
            null, false, LayoutMode.Open.ToString(), null, null, null, null,
            null, null, venueIds[9], organizerId, null);
        
        await ticketTypeProc.CreateAsync(draftEventId, "Standard Enrollment", 29900, 2000, 30, 0, "Includes all course materials and daily lunch.");
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
