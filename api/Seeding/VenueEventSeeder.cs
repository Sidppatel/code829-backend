using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds 10 real Mobile/Gulf Coast AL venues and ~10 events using Grid or Open layout modes.
/// Grid events: price per table (tables created by LayoutSeeder).
/// Open events: price per person with MaxCapacity.
/// </summary>
public static class VenueEventSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();

        if (await context.Venues.AnyAsync())
            return;

        var organizer = await context.Users.FirstAsync(u => u.Email == "organizer@code829.local");
        var admin = await context.Users.FirstAsync(u => u.Email == "admin@code829.local");

        var venues = SeedVenues(context);
        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} venues", venues.Count);

        SeedEvents(context, venues, organizer.Id, admin.Id);
        await context.SaveChangesAsync();

        var eventCount = await context.Events.CountAsync();
        Log.Information("[Seed] Created {Count} events", eventCount);
    }

    private static List<Venue> SeedVenues(EventPlatformDbContext context)
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

        var venues = new List<Venue>();
        foreach (var (name, line1, city, state, zip, phone, email, website, desc) in venueData)
        {
            var address = new Address
            {
                Id = Guid.NewGuid(),
                Line1 = line1,
                City = city,
                State = state,
                ZipCode = zip
            };
            context.Addresses.Add(address);

            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = name,
                AddressId = address.Id,
                Address = address,
                Description = desc,
                Phone = phone,
                Email = email,
                Website = website,
            };
            venues.Add(venue);
        }

        context.Venues.AddRange(venues);
        return venues;
    }

    private static void SeedEvents(EventPlatformDbContext context, List<Venue> venues, Guid organizerId, Guid adminId)
    {
        var now = DateTime.UtcNow;

        // ──────────────────────────────────────────────────────────────
        // Grid events — price per table, tables created by LayoutSeeder
        // ──────────────────────────────────────────────────────────────
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
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Title = title,
                Slug = GenerateSlug(title),
                Description = desc,
                Status = status,
                Category = cat,
                StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(startDate.AddHours(4), DateTimeKind.Utc),
                IsFeatured = featured,
                LayoutMode = LayoutMode.Grid,
                GridRows = rows,
                GridCols = cols,
                PublishedAt = status == EventStatus.Published
                    ? DateTime.UtcNow.AddDays(-7) : null,
                VenueId = venues[venueIdx].Id,
                OrganizerId = orgId
            };
            context.Events.Add(ev);
        }

        // ──────────────────────────────────────────────────────────────
        // Open events — price per person with MaxCapacity
        // ──────────────────────────────────────────────────────────────
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
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Title = title,
                Slug = GenerateSlug(title),
                Description = desc,
                Status = status,
                Category = cat,
                StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(startDate.AddHours(4), DateTimeKind.Utc),
                IsFeatured = featured,
                LayoutMode = LayoutMode.Open,
                PricePerPersonCents = pricePerPerson,
                MaxCapacity = maxCap,
                PublishedAt = status == EventStatus.Published
                    ? DateTime.UtcNow.AddDays(-7) : null,
                VenueId = venues[venueIdx].Id,
                OrganizerId = orgId
            };
            context.Events.Add(ev);
        }

        // ──────────────────────────────────────────────────────────────
        // Draft events (1 Grid, 1 Open)
        // ──────────────────────────────────────────────────────────────
        var draftGrid = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Mobile Mardi Gras Preview Ball",
            Slug = GenerateSlug("Mobile Mardi Gras Preview Ball"),
            Description = "Exclusive preview of the upcoming Mardi Gras season with floats, royalty, and brass bands.",
            Status = EventStatus.Draft,
            Category = EventCategory.Social,
            StartDate = DateTime.SpecifyKind(now.AddDays(49).Date.AddHours(19), DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(now.AddDays(49).Date.AddHours(23), DateTimeKind.Utc),
            LayoutMode = LayoutMode.Grid,
            GridRows = 6,
            GridCols = 8,
            VenueId = venues[1].Id,
            OrganizerId = adminId
        };
        context.Events.Add(draftGrid);

        var draftOpen = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Summer Coding Bootcamp for Kids",
            Slug = GenerateSlug("Summer Coding Bootcamp for Kids"),
            Description = "Two-week intensive coding program for ages 10-16. Learn Python, web development, and game design.",
            Status = EventStatus.Draft,
            Category = EventCategory.Tech,
            StartDate = DateTime.SpecifyKind(now.AddDays(56).Date.AddHours(9), DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(now.AddDays(56).Date.AddHours(16), DateTimeKind.Utc),
            LayoutMode = LayoutMode.Open,
            PricePerPersonCents = 29900,
            MaxCapacity = 30,
            VenueId = venues[9].Id,
            OrganizerId = organizerId
        };
        context.Events.Add(draftOpen);
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
