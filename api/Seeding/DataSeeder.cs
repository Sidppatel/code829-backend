using Api.Services;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds initial users, app settings, and table templates on first run.
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
        await SeedTableTemplatesAsync(context);
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
            ["stripe_secret_key"] = ("MOCK_DEV", "Stripe secret key (sk_test_... or sk_live_...)"),
            ["stripe_publishable_key"] = ("MOCK_DEV", "Stripe publishable key (pk_test_... or pk_live_...)"),
            ["stripe_webhook_secret"] = ("MOCK_DEV", "Stripe webhook signing secret (whsec_...)"),
            ["smtp_host"] = ("MOCK_DEV", "SMTP server hostname (e.g. smtp.gmail.com)"),
            ["smtp_port"] = ("587", "SMTP port: 587 for TLS/STARTTLS, 465 for SSL"),
            ["smtp_username"] = ("MOCK_DEV", "SMTP username (e.g. yourname@gmail.com)"),
            ["smtp_password"] = ("MOCK_DEV", "SMTP password or app password (16-char for Gmail)"),
            ["resend_api_key"] = ("MOCK_DEV", "Resend API key for sending emails (re_...)"),
            ["email_from_address"] = ("noreply@code829.local", "Sender email address (defaults to smtp_username if empty)"),
            ["platform_fee_percent"] = ("8", "Platform fee percentage on ticket price"),
            ["platform_fee_flat_cents"] = ("0", "Flat fee per booking in cents"),
            ["default_platform_fee_cents"] = ("1500", "Default flat platform fee per booking in cents ($15.00)"),
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

        // In production, ensure frontend_url and cors_origins point to the real deployment
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env == "Production")
        {
            var frontendUrl = await settings.GetOrDefaultAsync("frontend_url");
            if (frontendUrl is null || frontendUrl.Contains("localhost"))
            {
                await settings.SetAsync("frontend_url", "https://code829.pages.dev", "Frontend URL for magic link emails");
                Log.Information("[Seed] Updated frontend_url to production URL");
            }

            var corsOrigins = await settings.GetOrDefaultAsync("cors_origins");
            if (corsOrigins is null || corsOrigins.Contains("localhost"))
            {
                await settings.SetAsync("cors_origins", "https://code829.pages.dev", "Comma-separated allowed CORS origins");
                Log.Information("[Seed] Updated cors_origins to production URL");
            }

            // Override email/Resend settings from environment variables (set in Render dashboard)
            var envOverrides = new (string EnvVar, string SettingKey, string Description)[]
            {
                ("RESEND_API_KEY", "resend_api_key", "Resend API key for sending emails"),
                ("EMAIL_FROM_ADDRESS", "email_from_address", "Sender email address"),
            };

            foreach (var (envVar, settingKey, description) in envOverrides)
            {
                var envValue = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(envValue))
                {
                    var current = await settings.GetOrDefaultAsync(settingKey);
                    if (current != envValue)
                    {
                        await settings.SetAsync(settingKey, envValue, description);
                        Log.Information("[Seed] Updated {SettingKey} from environment", settingKey);
                    }
                }
            }
        }

        Log.Information("[Seed] App settings initialized");
    }

    private static async Task SeedTableTemplatesAsync(EventPlatformDbContext context)
    {
        if (await context.TableTemplates.AnyAsync())
            return;

        var templates = new[]
        {
            new TableTemplate { Id = Guid.NewGuid(), Name = "Standard Round (4)", DefaultCapacity = 4, DefaultShape = TableShape.Round, DefaultColor = "#4f46e5", DefaultPriceCents = 10000, IsActive = true },
            new TableTemplate { Id = Guid.NewGuid(), Name = "VIP Rectangle (6)", DefaultCapacity = 6, DefaultShape = TableShape.Rectangle, DefaultColor = "#7c3aed", DefaultPriceCents = 15000, IsActive = true },
            new TableTemplate { Id = Guid.NewGuid(), Name = "Cocktail Highboy (2)", DefaultCapacity = 2, DefaultShape = TableShape.Cocktail, DefaultColor = "#f97316", DefaultPriceCents = 12000, IsActive = true },
            new TableTemplate { Id = Guid.NewGuid(), Name = "Lounge Section (8)", DefaultCapacity = 8, DefaultShape = TableShape.Square, DefaultColor = "#22c55e", DefaultPriceCents = 18000, IsActive = true },
        };

        context.TableTemplates.AddRange(templates);
        await context.SaveChangesAsync();
        Log.Information("[Seed] Created {Count} default table templates", templates.Length);
    }
}
