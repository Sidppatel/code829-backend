using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds 10 real Mobile, AL venues and 18 events across 8 categories
/// with 2-4 ticket tiers each. Events span 2-8 weeks from current date.
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
        var ticketCount = await context.TicketTypes.CountAsync();
        Log.Information("[Seed] Created {Events} events with {Tickets} ticket types", eventCount, ticketCount);
    }

    private static List<Venue> SeedVenues(EventPlatformDbContext context)
    {
        var venues = new List<Venue>
        {
            new()
            {
                Id = Guid.NewGuid(), Name = "The Saenger Theatre",
                Address = "6 S Joachim St", City = "Mobile", State = "AL", ZipCode = "36602",
                Description = "Historic 1927 theatre featuring Spanish Baroque architecture, hosting concerts, Broadway shows, and special events in downtown Mobile.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "Mobile Convention Center",
                Address = "1 S Water St", City = "Mobile", State = "AL", ZipCode = "36602",
                Description = "Premier convention facility on the waterfront with flexible event spaces for conferences, expos, and large gatherings.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "The Blind Mule",
                Address = "57 N Claiborne St", City = "Mobile", State = "AL", ZipCode = "36602",
                Description = "Intimate downtown venue and gastropub known for craft cocktails, local music, and a vibrant nightlife atmosphere.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "Moe's Original BBQ",
                Address = "6423 Old Shell Rd", City = "Mobile", State = "AL", ZipCode = "36608",
                Description = "Alabama-style BBQ joint with a laid-back patio and stage for live blues, country, and Americana acts.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "The Soul Kitchen",
                Address = "219 Dauphin St", City = "Mobile", State = "AL", ZipCode = "36602",
                Description = "Eclectic Dauphin Street venue combining Southern cuisine with live music, poetry nights, and community events.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "Hank Aaron Stadium Area",
                Address = "755 Bolling Brothers Blvd", City = "Mobile", State = "AL", ZipCode = "36606",
                Description = "Open-air stadium complex hosting sporting events, festivals, and large outdoor concerts in Mobile.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "Bellingrath Gardens",
                Address = "12401 Bellingrath Gardens Rd", City = "Theodore", State = "AL", ZipCode = "36582",
                Description = "Stunning 65-acre garden estate south of Mobile offering outdoor events surrounded by azaleas, roses, and live oaks.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "OWA Amusement Park",
                Address = "1501 S OWA Blvd", City = "Foley", State = "AL", ZipCode = "36535",
                Description = "Family entertainment destination on the Gulf Coast with rides, dining, and a bustling downtown district for events.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "The Wharf Amphitheatre",
                Address = "4830 Main St", City = "Orange Beach", State = "AL", ZipCode = "36561",
                Description = "Premier outdoor amphitheatre on the Alabama Gulf Coast hosting national touring acts and major festivals.",
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "Fairhope Civic Center",
                Address = "161 N Section St", City = "Fairhope", State = "AL", ZipCode = "36532",
                Description = "Charming civic center in the arts community of Fairhope, hosting lectures, dances, and cultural events on Mobile Bay.",
            }
        };

        context.Venues.AddRange(venues);
        return venues;
    }

    private static void SeedEvents(EventPlatformDbContext context, List<Venue> venues, Guid organizerId, Guid adminId)
    {
        var now = DateTime.UtcNow;

        var events = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, (string Name, int Price, int Qty)[] Tickets)[]
        {
            ("Gulf Coast Jazz Night", "An evening of smooth jazz featuring Gulf Coast musicians. Enjoy craft cocktails and Southern appetizers under the stars.", EventCategory.Music, EventStatus.Published, 0, organizerId, 2, true,
                [("General Admission", 3500, 500), ("VIP Lounge", 7500, 100), ("Student", 2000, 200)]),

            ("Mobile Tech Summit 2026", "Explore the latest in AI, cloud computing, and startup innovation at Mobile's premier tech conference.", EventCategory.Tech, EventStatus.Published, 1, adminId, 3, true,
                [("Early Bird", 4900, 300), ("General", 7900, 500), ("VIP + Workshop", 14900, 100), ("Student Pass", 2500, 200)]),

            ("Downtown Blues & BBQ Fest", "Live blues music paired with the best BBQ the Gulf Coast has to offer. Family-friendly outdoor festival.", EventCategory.Music, EventStatus.Published, 3, organizerId, 2, false,
                [("General Admission", 2500, 150), ("Pit Master VIP", 5500, 30)]),

            ("Fairhope Arts Walk", "Stroll through galleries, meet local artists, and enjoy live painting demonstrations in charming downtown Fairhope.", EventCategory.Arts, EventStatus.Published, 9, adminId, 4, false,
                [("General Admission", 0, 300), ("Artist Meet & Greet", 2500, 50)]),

            ("Gulf Shores Summer Kickoff Concert", "National headliner TBA. The biggest outdoor concert of the summer on the Alabama Gulf Coast.", EventCategory.Music, EventStatus.Published, 8, organizerId, 6, true,
                [("Lawn", 4500, 5000), ("Reserved Seating", 7500, 2000), ("Pit Access", 12000, 500), ("VIP Box", 25000, 100)]),

            ("Mobile Business Leaders Luncheon", "Quarterly networking luncheon for Mobile's business community. Keynote speaker from the chamber of commerce.", EventCategory.Business, EventStatus.Published, 1, adminId, 3, false,
                [("Member", 3500, 200), ("Non-Member", 5500, 100), ("Table of 8", 24000, 20)]),

            ("Blind Mule Comedy Night", "Stand-up comedy showcase featuring rising Southern comedians. Two-drink minimum. Ages 21+.", EventCategory.Social, EventStatus.Published, 2, organizerId, 2, false,
                [("General Admission", 2000, 120), ("Front Row", 3500, 20)]),

            ("Bellingrath Gardens Spring Gala", "Elegant evening fundraiser among the azaleas. Live orchestra, gourmet dinner, and silent auction.", EventCategory.Social, EventStatus.Published, 6, adminId, 5, true,
                [("Individual", 15000, 200), ("Couple", 25000, 100), ("Patron Table", 100000, 20)]),

            ("Kids' Adventure Day at OWA", "A day of rides, face painting, magic shows, and family fun at OWA Amusement Park.", EventCategory.Family, EventStatus.Published, 7, organizerId, 4, false,
                [("Child (3-12)", 2500, 800), ("Adult", 1500, 600), ("Family Pack (2+2)", 7000, 200)]),

            ("Soul Kitchen Open Mic & Poetry Slam", "Bring your best verses and original songs to Dauphin Street's most soulful stage.", EventCategory.Arts, EventStatus.Published, 4, organizerId, 2, false,
                [("General Admission", 1000, 100), ("Performer Entry", 500, 20)]),

            ("Mobile Bay Seafood Cook-Off", "Chefs from across the Gulf compete for the Golden Shrimp trophy. Tastings included with admission.", EventCategory.Dining, EventStatus.Published, 5, adminId, 5, false,
                [("Tasting Pass", 4500, 2000), ("VIP Tasting + Judging", 9500, 150), ("Kids (under 12)", 1500, 500)]),

            ("Saenger Classic Film Series: Casablanca", "Watch Casablanca on the big screen in the beautifully restored 1927 Saenger Theatre.", EventCategory.Arts, EventStatus.Published, 0, adminId, 3, false,
                [("Orchestra", 2000, 800), ("Balcony", 1500, 600), ("Senior/Student", 1000, 200)]),

            ("Gulf Coast Startup Pitch Night", "Ten startups pitch to a panel of angel investors. Network with Mobile's entrepreneurial community.", EventCategory.Tech, EventStatus.Published, 2, organizerId, 4, false,
                [("Audience", 1500, 100), ("Pitcher Entry", 5000, 10)]),

            ("5K Run for the Bay", "Scenic 5K along Mobile Bay supporting coastal conservation. Post-race party with live music.", EventCategory.Sports, EventStatus.Published, 5, organizerId, 6, false,
                [("Adult Runner", 3500, 1500), ("Youth Runner (under 18)", 2000, 500), ("Fun Walk", 1500, 300)]),

            ("Farm-to-Table Dinner: Spring Harvest", "Multi-course dinner featuring local farms and Gulf seafood, served under the oaks at Bellingrath.", EventCategory.Dining, EventStatus.Published, 6, adminId, 3, false,
                [("Dinner Seat", 8500, 80), ("Dinner + Wine Pairing", 12000, 40)]),

            // Draft events
            ("Mobile Mardi Gras Preview Ball", "Exclusive preview of the upcoming Mardi Gras season with floats, royalty, and brass bands.", EventCategory.Social, EventStatus.Draft, 1, adminId, 7, false,
                [("General", 5000, 1000), ("Mystic Society", 15000, 200)]),

            ("Summer Coding Bootcamp for Kids", "Two-week intensive coding program for ages 10-16. Learn Python, web development, and game design.", EventCategory.Tech, EventStatus.Draft, 9, organizerId, 8, false,
                [("Full Program", 29900, 30), ("Day Pass", 5000, 20)]),

            // Completed event (past)
            ("Mardi Gras Music Marathon", "A completed 12-hour music marathon celebrating Mobile's Mardi Gras heritage with local bands.", EventCategory.Music, EventStatus.Completed, 0, organizerId, -2, false,
                [("General", 2000, 1200), ("VIP", 5000, 200)])
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, tickets) in events)
        {
            var startDate = now.AddDays(weeksOut * 7).Date.AddHours(18); // 6 PM
            var endDate = startDate.AddHours(4);

            var slug = GenerateSlug(title);
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Title = title,
                Slug = slug,
                Description = desc,
                Status = status,
                Category = cat,
                StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc),
                IsFeatured = featured,
                VenueId = venues[venueIdx].Id,
                OrganizerId = orgId
            };
            context.Events.Add(ev);

            for (var i = 0; i < tickets.Length; i++)
            {
                var (name, price, qty) = tickets[i];
                context.TicketTypes.Add(new TicketType
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    PriceCents = price,
                    QuantityTotal = qty,
                    QuantitySold = 0,
                    SortOrder = i,
                    EventId = ev.Id
                });
            }
        }
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
