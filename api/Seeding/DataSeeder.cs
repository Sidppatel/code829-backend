using Api.Services;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds initial users and app settings on first run.
/// Only seeds if the users table is empty to avoid duplicates.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        await SeedUsersAsync(context, encryption);
        await SeedSettingsAsync(settingsService);
        await SeedTableTypesAsync(context);
        await SeedTicketTypeTemplatesAsync(context);
        await SeedPricingRuleTemplatesAsync(context);
        await SeedEventTemplatesAsync(context);
    }

    private static async Task SeedUsersAsync(EventPlatformDbContext context, IEncryptionService encryption)
    {
        if (await context.Users.AnyAsync())
            return;

        var users = new (string Email, string FirstName, string LastName, UserRole Role)[]
        {
            ("developer@code829.local", "Dev", "Admin", UserRole.Developer),
            ("admin@code829.local", "Sarah", "Mitchell", UserRole.Admin),
            ("staff@code829.local", "Marcus", "Johnson", UserRole.Staff),
            ("user@code829.local", "Jamie", "Rivera", UserRole.User),
            ("user2@code829.local", "Taylor", "Brooks", UserRole.User),
            ("user3@code829.local", "Alex", "Chen", UserRole.User),
            ("organizer@code829.local", "Gulf Events", "Co.", UserRole.Admin),
        };

        foreach (var (email, firstName, lastName, role) in users)
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                EmailHash = encryption.HashEmail(email),
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} users", users.Length);
    }

    private static async Task SeedSettingsAsync(ISettingsService settings)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["jwt_secret"] = (Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), "JWT signing secret"),
            ["magic_link_expiry_minutes"] = ("15", "Magic link token lifetime in minutes"),
            ["hold_expiry_minutes"] = ("10", "Seat hold duration in minutes"),
            ["stripe_secret_key"] = ("MOCK_DEV", "Stripe secret key (mocked in dev)"),
            ["email_api_key"] = ("MOCK_DEV", "Email API key (mocked in dev)"),
            ["email_from_address"] = ("noreply@code829.local", "Sender email address"),
            ["platform_fee_percent"] = ("8", "Platform fee percentage on ticket price"),
            ["platform_fee_flat_cents"] = ("0", "Flat fee per booking in cents"),
            ["frontend_url"] = ("http://localhost:5173", "Frontend URL for magic link emails"),
            ["cors_origins"] = ("http://localhost:5173", "Comma-separated allowed CORS origins"),
            ["brand_name"] = ("Code829", "White-label brand name"),
            ["brand_tagline"] = ("Where Great Events Come to Life", "Brand tagline"),
            ["brand_primary_color"] = ("#4f46e5", "Primary brand color"),
            ["brand_accent_color"] = ("#f97316", "CTA/accent color"),
            ["default_theme"] = ("system", "Default theme: light, dark, or system"),
            ["default_city"] = ("Mobile", "Default event city"),
            ["default_state"] = ("AL", "Default state"),
            ["default_timezone"] = ("America/Chicago", "Default timezone"),
            ["search_results_per_page"] = ("20", "Search pagination page size"),
            ["max_tickets_per_booking"] = ("10", "Maximum tickets per booking"),
            ["dev_log_retention_days"] = ("90", "Developer log retention in days"),
            ["admin_log_retention_days"] = ("365", "Admin log retention in days"),
            ["system_log_retention_days"] = ("30", "System log retention in days"),
        };

        foreach (var (key, (value, description)) in defaults)
        {
            var existing = await settings.GetOrDefaultAsync(key);
            if (existing is null)
            {
                await settings.SetAsync(key, value, description);
            }
        }

        Log.Information("[Seed] App settings initialized");
    }

    private static async Task SeedTableTypesAsync(EventPlatformDbContext context)
    {
        if (await context.TableTypes.AnyAsync(tt => tt.VenueId == null))
            return;

        var types = new[]
        {
            new TableType { Id = Guid.NewGuid(), Name = "Standard Round (4)", DefaultCapacity = 4, DefaultShape = Contracts.Enums.TableShape.Round, DefaultColor = "#4f46e5", DefaultPriceCents = 0, IsActive = true },
            new TableType { Id = Guid.NewGuid(), Name = "VIP Rectangle (6)", DefaultCapacity = 6, DefaultShape = Contracts.Enums.TableShape.Rectangle, DefaultColor = "#7c3aed", DefaultPriceCents = 0, IsActive = true },
            new TableType { Id = Guid.NewGuid(), Name = "Cocktail Highboy (2)", DefaultCapacity = 2, DefaultShape = Contracts.Enums.TableShape.Cocktail, DefaultColor = "#f97316", DefaultPriceCents = 0, IsActive = true },
            new TableType { Id = Guid.NewGuid(), Name = "Lounge Section (8)", DefaultCapacity = 8, DefaultShape = Contracts.Enums.TableShape.Square, DefaultColor = "#22c55e", DefaultPriceCents = 0, IsActive = true },
        };

        context.TableTypes.AddRange(types);
        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} default table types", types.Length);
    }

    private static async Task SeedTicketTypeTemplatesAsync(EventPlatformDbContext context)
    {
        if (await context.TicketTypeTemplates.AnyAsync())
            return;

        var templates = new[]
        {
            new TicketTypeTemplate { Id = Guid.NewGuid(), Name = "General Admission", Description = "Standard entry ticket", DefaultPriceCents = 3500, DefaultPlatformFeeCents = 280, IsActive = true, SortOrder = 0 },
            new TicketTypeTemplate { Id = Guid.NewGuid(), Name = "VIP", Description = "Premium access with perks", DefaultPriceCents = 7500, DefaultPlatformFeeCents = 600, IsActive = true, SortOrder = 1 },
            new TicketTypeTemplate { Id = Guid.NewGuid(), Name = "Student", Description = "Discounted student ticket (valid ID required)", DefaultPriceCents = 2000, DefaultPlatformFeeCents = 160, IsActive = true, SortOrder = 2 },
            new TicketTypeTemplate { Id = Guid.NewGuid(), Name = "Early Bird", Description = "Limited early purchase discount", DefaultPriceCents = 2500, DefaultPlatformFeeCents = 200, IsActive = true, SortOrder = 3 },
        };

        context.TicketTypeTemplates.AddRange(templates);
        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} ticket type templates", templates.Length);
    }

    private static async Task SeedPricingRuleTemplatesAsync(EventPlatformDbContext context)
    {
        if (await context.PricingRuleTemplates.AnyAsync())
            return;

        var templates = new[]
        {
            new PricingRuleTemplate { Id = Guid.NewGuid(), Name = "Standard", Type = PricingRuleType.Standard, DefaultPriceCents = 3500, Description = "Default pricing rule", IsActive = true, SortOrder = 0 },
            new PricingRuleTemplate { Id = Guid.NewGuid(), Name = "Early Bird", Type = PricingRuleType.EarlyBird, DefaultPriceCents = 2000, Description = "Early purchase discount", IsActive = true, SortOrder = 1 },
            new PricingRuleTemplate { Id = Guid.NewGuid(), Name = "First 50 Tickets", Type = PricingRuleType.FirstN, DefaultPriceCents = 1500, Description = "Discount for first 50 purchases", IsActive = true, SortOrder = 2 },
        };

        context.PricingRuleTemplates.AddRange(templates);
        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} pricing rule templates", templates.Length);
    }

    private static async Task SeedEventTemplatesAsync(EventPlatformDbContext context)
    {
        if (await context.EventTemplates.AnyAsync())
            return;

        var templates = new[]
        {
            new EventTemplate { Id = Guid.NewGuid(), Name = "Concert", Description = "Live music event template", Category = EventCategory.Music, LayoutMode = LayoutMode.Grid, DefaultMaxCapacity = 500, DefaultPlatformFeePercent = 8, IsActive = true },
            new EventTemplate { Id = Guid.NewGuid(), Name = "Conference", Description = "Business/tech conference template", Category = EventCategory.Tech, LayoutMode = LayoutMode.CapacityOnly, DefaultMaxCapacity = 300, DefaultPlatformFeePercent = 10, IsActive = true },
            new EventTemplate { Id = Guid.NewGuid(), Name = "Social Gathering", Description = "Social event template", Category = EventCategory.Social, LayoutMode = LayoutMode.None, DefaultMaxCapacity = 200, DefaultPlatformFeePercent = 8, IsActive = true },
            new EventTemplate { Id = Guid.NewGuid(), Name = "Dining Experience", Description = "Food and dining event template", Category = EventCategory.Dining, LayoutMode = LayoutMode.Grid, DefaultMaxCapacity = 100, DefaultPlatformFeePercent = 8, IsActive = true },
        };

        context.EventTemplates.AddRange(templates);
        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} event templates", templates.Length);
    }
}
