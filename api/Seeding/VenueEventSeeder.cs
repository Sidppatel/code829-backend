using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds 10 real Mobile, AL venues and 18 events across 8 categories.
/// Each event has a definite LayoutMode set here — Grid events get table-based
/// ticket types, non-Grid events get standard ticket tiers.
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

        await SeedVenueLayoutsAsync(context, venues);

        SeedEvents(context, venues, organizer.Id, admin.Id);
        await context.SaveChangesAsync();

        var eventCount = await context.Events.CountAsync();
        var ticketCount = await context.TicketTypes.CountAsync();
        Log.Information("[Seed] Created {Events} events with {Tickets} ticket types", eventCount, ticketCount);
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
        // Grid (Assigned Seating) events — dining, galas, luncheons
        // These get a single "Table Reservation" ticket type.
        // Actual pricing is per-table in LayoutPricingSeeder.
        // ──────────────────────────────────────────────────────────────
        var gridEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, string TicketName, int TicketPrice, int TicketQty)[]
        {
            ("Bellingrath Gardens Spring Gala", "Elegant evening fundraiser among the azaleas. Live orchestra, gourmet dinner, and silent auction.",
                EventCategory.Social, EventStatus.Published, 6, adminId, 5, true, "Gala Reservation", 15000, 320),
            ("Farm-to-Table Dinner: Spring Harvest", "Multi-course dinner featuring local farms and Gulf seafood, served under the oaks at Bellingrath.",
                EventCategory.Dining, EventStatus.Published, 6, adminId, 3, false, "Dinner Reservation", 8500, 120),
            ("Mobile Business Leaders Luncheon", "Quarterly networking luncheon for Mobile's business community. Keynote speaker from the chamber of commerce.",
                EventCategory.Business, EventStatus.Published, 1, adminId, 3, false, "Luncheon Seat", 3500, 160),
            ("Blind Mule Comedy Night", "Stand-up comedy showcase featuring rising Southern comedians. Two-drink minimum. Ages 21+.",
                EventCategory.Social, EventStatus.Published, 2, organizerId, 2, false, "Comedy Night Seat", 2000, 80),
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, ticketName, ticketPrice, ticketQty) in gridEvents)
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
                EditorMode = EditorMode.Grid,
                VenueId = venues[venueIdx].Id,
                OrganizerId = orgId
            };
            context.Events.Add(ev);

            context.TicketTypes.Add(new TicketType
            {
                Id = Guid.NewGuid(),
                Name = ticketName,
                PriceCents = ticketPrice,
                QuantityTotal = ticketQty,
                QuantitySold = 0,
                SortOrder = 0,
                EventId = ev.Id
            });
        }

        // ──────────────────────────────────────────────────────────────
        // CapacityOnly (General Admission) events — concerts, festivals
        // ──────────────────────────────────────────────────────────────
        var capacityEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, int MaxCap, (string Name, int Price, int Qty)[] Tickets)[]
        {
            ("Gulf Coast Jazz Night", "An evening of smooth jazz featuring Gulf Coast musicians. Enjoy craft cocktails and Southern appetizers under the stars.",
                EventCategory.Music, EventStatus.Published, 0, organizerId, 2, true, 800,
                [("General Admission", 3500, 500), ("VIP Lounge", 7500, 100), ("Student", 2000, 200)]),
            ("Gulf Shores Summer Kickoff Concert", "National headliner TBA. The biggest outdoor concert of the summer on the Alabama Gulf Coast.",
                EventCategory.Music, EventStatus.Published, 8, organizerId, 6, true, 7600,
                [("Lawn", 4500, 5000), ("Reserved Seating", 7500, 2000), ("Pit Access", 12000, 500), ("VIP Box", 25000, 100)]),
            ("Downtown Blues & BBQ Fest", "Live blues music paired with the best BBQ the Gulf Coast has to offer. Family-friendly outdoor festival.",
                EventCategory.Music, EventStatus.Published, 3, organizerId, 2, false, 180,
                [("General Admission", 2500, 150), ("Pit Master VIP", 5500, 30)]),
            ("Mobile Bay Seafood Cook-Off", "Chefs from across the Gulf compete for the Golden Shrimp trophy. Tastings included with admission.",
                EventCategory.Dining, EventStatus.Published, 5, adminId, 5, false, 2650,
                [("Tasting Pass", 4500, 2000), ("VIP Tasting + Judging", 9500, 150), ("Kids (under 12)", 1500, 500)]),
            ("Kids' Adventure Day at OWA", "A day of rides, face painting, magic shows, and family fun at OWA Amusement Park.",
                EventCategory.Family, EventStatus.Published, 7, organizerId, 4, false, 1600,
                [("Child (3-12)", 2500, 800), ("Adult", 1500, 600), ("Family Pack (2+2)", 7000, 200)]),
            ("5K Run for the Bay", "Scenic 5K along Mobile Bay supporting coastal conservation. Post-race party with live music.",
                EventCategory.Sports, EventStatus.Published, 5, organizerId, 6, false, 2300,
                [("Adult Runner", 3500, 1500), ("Youth Runner (under 18)", 2000, 500), ("Fun Walk", 1500, 300)]),
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, maxCap, tickets) in capacityEvents)
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
                LayoutMode = LayoutMode.CapacityOnly,
                MaxCapacity = maxCap,
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

        // ──────────────────────────────────────────────────────────────
        // None (Tickets Only) events — workshops, online, simple
        // ──────────────────────────────────────────────────────────────
        var noneEvents = new (string Title, string Desc, EventCategory Cat, EventStatus Status, int VenueIdx, Guid OrgId, int WeeksOut, bool Featured, (string Name, int Price, int Qty)[] Tickets)[]
        {
            ("Mobile Tech Summit 2026", "Explore the latest in AI, cloud computing, and startup innovation at Mobile's premier tech conference.",
                EventCategory.Tech, EventStatus.Published, 1, adminId, 3, true,
                [("Early Bird", 4900, 300), ("General", 7900, 500), ("VIP + Workshop", 14900, 100), ("Student Pass", 2500, 200)]),
            ("Fairhope Arts Walk", "Stroll through galleries, meet local artists, and enjoy live painting demonstrations in charming downtown Fairhope.",
                EventCategory.Arts, EventStatus.Published, 9, adminId, 4, false,
                [("General Admission", 0, 300), ("Artist Meet & Greet", 2500, 50)]),
            ("Soul Kitchen Open Mic & Poetry Slam", "Bring your best verses and original songs to Dauphin Street's most soulful stage.",
                EventCategory.Arts, EventStatus.Published, 4, organizerId, 2, false,
                [("General Admission", 1000, 100), ("Performer Entry", 500, 20)]),
            ("Saenger Classic Film Series: Casablanca", "Watch Casablanca on the big screen in the beautifully restored 1927 Saenger Theatre.",
                EventCategory.Arts, EventStatus.Published, 0, adminId, 3, false,
                [("Orchestra", 2000, 800), ("Balcony", 1500, 600), ("Senior/Student", 1000, 200)]),
            ("Gulf Coast Startup Pitch Night", "Ten startups pitch to a panel of angel investors. Network with Mobile's entrepreneurial community.",
                EventCategory.Tech, EventStatus.Published, 2, organizerId, 4, false,
                [("Audience", 1500, 100), ("Pitcher Entry", 5000, 10)]),
            // Draft events
            ("Mobile Mardi Gras Preview Ball", "Exclusive preview of the upcoming Mardi Gras season with floats, royalty, and brass bands.",
                EventCategory.Social, EventStatus.Draft, 1, adminId, 7, false,
                [("General", 5000, 1000), ("Mystic Society", 15000, 200)]),
            ("Summer Coding Bootcamp for Kids", "Two-week intensive coding program for ages 10-16. Learn Python, web development, and game design.",
                EventCategory.Tech, EventStatus.Draft, 9, organizerId, 8, false,
                [("Full Program", 29900, 30), ("Day Pass", 5000, 20)]),
            // Completed event (past)
            ("Mardi Gras Music Marathon", "A completed 12-hour music marathon celebrating Mobile's Mardi Gras heritage with local bands.",
                EventCategory.Music, EventStatus.Completed, 0, organizerId, -2, false,
                [("General", 2000, 1200), ("VIP", 5000, 200)]),
        };

        foreach (var (title, desc, cat, status, venueIdx, orgId, weeksOut, featured, tickets) in noneEvents)
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
                LayoutMode = LayoutMode.None,
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

    private static async Task SeedVenueLayoutsAsync(EventPlatformDbContext context, List<Venue> venues)
    {
        var tableTypes = await context.TableTypes.Where(tt => tt.VenueId == null).ToListAsync();
        if (tableTypes.Count == 0) return;

        var roundType = tableTypes.First(t => t.DefaultShape == TableShape.Round);
        var rectType = tableTypes.First(t => t.DefaultShape == TableShape.Rectangle);

        var layoutConfigs = new (int VenueIdx, string Name, int Rows, int Cols)[]
        {
            (6, "Bellingrath Pavilion", 6, 8),
            (1, "Convention Hall Grid", 8, 10),
            (2, "Bar Floor Plan", 4, 5),
        };

        foreach (var (venueIdx, name, rows, cols) in layoutConfigs)
        {
            var layout = new VenueLayout
            {
                Id = Guid.NewGuid(),
                Name = name,
                LayoutMode = LayoutMode.Grid,
                EditorMode = EditorMode.Grid,
                GridRows = rows,
                GridCols = cols,
                IsDefault = true,
                IsActive = true,
                VenueId = venues[venueIdx].Id
            };
            context.VenueLayouts.Add(layout);

            for (var i = 0; i < Math.Min(6, rows * cols); i++)
            {
                var row = i / cols;
                var col = i % cols;
                var isVip = i < 2;
                var colLetter = (char)('A' + col);

                context.VenueLayoutTables.Add(new VenueLayoutTable
                {
                    Id = Guid.NewGuid(),
                    Label = $"{colLetter}{row + 1}",
                    Section = isVip ? "VIP" : "Standard",
                    GridRow = row,
                    GridCol = col,
                    SortOrder = i,
                    PriceType = PriceType.PerTable,
                    PriceCents = isVip ? 15000 : 7500,
                    IsActive = true,
                    VenueLayoutId = layout.Id,
                    TableTypeId = isVip ? rectType.Id : roundType.Id
                });
            }
        }

        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} venue layouts", layoutConfigs.Length);
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
