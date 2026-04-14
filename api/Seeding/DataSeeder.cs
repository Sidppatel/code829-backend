using Api.Services;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
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
        var authProc = scope.ServiceProvider.GetRequiredService<IAuthProcedures>();
        var adminProc = scope.ServiceProvider.GetRequiredService<IAdminUserProcedures>();

        await SeedAdminUsersAsync(context, encryption, adminProc);
        await SeedUsersAsync(context, encryption, authProc);
        await SeedSettingsAsync(settingsService);
        await SeedTableTemplatesAsync(context);
    }

    private static async Task SeedAdminUsersAsync(EventPlatformDbContext context, IEncryptionService encryption, IAdminUserProcedures adminProc)
    {
        if (await context.AdminUsers.AnyAsync())
            return;

        var admins = new (string Email, string FirstName, string LastName, AdminRole Role, string Password)[]
        {
            ("developer@code829.local", "Dev", "Admin", AdminRole.Developer, "Dev@12345"),
            ("admin@code829.local", "Sarah", "Mitchell", AdminRole.Admin, "Admin@12345"),
            ("staff@code829.local", "Marcus", "Johnson", AdminRole.Staff, "Staff@12345"),
            ("organizer@code829.local", "Gulf Events", "Co.", AdminRole.Admin, "Admin@12345"),
        };

        foreach (var (email, firstName, lastName, role, password) in admins)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            await adminProc.CreateAsync(email, encryption.HashEmail(email), firstName, lastName, hash, role.ToString());
        }

        Log.Information("[Seed] Created {Count} admin users via SP", admins.Length);
    }

    private static async Task SeedUsersAsync(EventPlatformDbContext context, IEncryptionService encryption, IAuthProcedures authProc)
    {
        if (await context.Users.AnyAsync())
            return;

        var users = new (string Email, string FirstName, string LastName)[]
        {
            ("user@code829.local", "Jamie", "Rivera"),
            ("user2@code829.local", "Taylor", "Brooks"),
            ("user3@code829.local", "Alex", "Chen"),
            ("luxury.guest@code829.local", "Sophia", "Vanderbilt"),
            ("tech.enthusiast@code829.local", "Jordan", "Lee"),
            ("local.foodie@code829.local", "Carla", "Sanchez"),
            ("music.lover@code829.local", "Miles", "Davis"),
            ("family.explorer@code829.local", "David", "Miller"),
        };

        foreach (var (email, firstName, lastName) in users)
        {
            await authProc.UpsertUserAsync(email, encryption.HashEmail(email), firstName, lastName);
        }

        Log.Information("[Seed] Created {Count} users via SP", users.Length);
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
            ["resend_api_key"] = ("MOCK_DEV", "Resend API key for sending emails (re_...)"),
            ["email_from_address"] = ("noreply@code829.local", "Sender email address"),
            ["app_name"] = ("Code829", "Application name used in emails and SEO"),
            ["default_platform_fee_cents"] = ("1500", "Default flat platform fee per booking in cents ($15.00)"),
            ["frontend_url"] = ("http://localhost:5173", "Frontend URL for magic link emails"),
            ["cors_origins"] = ("http://localhost:5173", "Comma-separated allowed CORS origins"),
            ["search_results_per_page"] = ("20", "Search pagination page size"),
            ["dev_log_retention_days"] = ("90", "Developer log retention in days"),
            ["admin_log_retention_days"] = ("365", "Admin log retention in days"),
            ["system_log_retention_days"] = ("30", "System log retention in days"),
            ["s3_bucket"] = ("MOCK_DEV", "Cloudflare R2 bucket name"),
            ["s3_access_key"] = ("MOCK_DEV", "Cloudflare R2 access key ID"),
            ["s3_secret_key"] = ("MOCK_DEV", "Cloudflare R2 secret access key"),
            ["s3_region"] = ("auto", "Cloudflare R2 region (always 'auto')"),
            ["s3_endpoint_url"] = ("MOCK_DEV", "Cloudflare R2 endpoint (https://<account-id>.r2.cloudflarestorage.com)"),
            ["cdn_base_url"] = ("MOCK_DEV", "Public CDN URL for serving uploaded images"),
        };

        foreach (var (key, (value, description)) in defaults)
        {
            var existing = await settings.GetOrDefaultAsync(key);
            if (existing is null)
            {
                await settings.SetAsync(key, value, description);
            }
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env == "Production")
        {
            var frontendUrl = await settings.GetOrDefaultAsync("frontend_url");
            if (frontendUrl is null || frontendUrl != "https://code829.com")
            {
                await settings.SetAsync("frontend_url", "https://code829.com", "Frontend URL for magic link emails");
                Log.Information("[Seed] Updated frontend_url to production URL");
            }

            var corsOrigins = await settings.GetOrDefaultAsync("cors_origins");
            if (corsOrigins is null || corsOrigins != "https://code829.com")
            {
                await settings.SetAsync("cors_origins", "https://code829.com", "Comma-separated allowed CORS origins");
                Log.Information("[Seed] Updated cors_origins to production URL");
            }

            var envOverrides = new (string EnvVar, string SettingKey, string Description)[]
            {
                ("RESEND_API_KEY", "resend_api_key", "Resend API key for sending emails"),
                ("EMAIL_FROM_ADDRESS", "email_from_address", "Sender email address"),
                ("STRIPE_SECRET_KEY", "stripe_secret_key", "Stripe secret key"),
                ("STRIPE_PUBLISHABLE_KEY", "stripe_publishable_key", "Stripe publishable key"),
                ("STRIPE_WEBHOOK_SECRET", "stripe_webhook_secret", "Stripe webhook signing secret"),
                ("S3_BUCKET", "s3_bucket", "Cloudflare R2 bucket name"),
                ("S3_ACCESS_KEY", "s3_access_key", "Cloudflare R2 access key ID"),
                ("S3_SECRET_KEY", "s3_secret_key", "Cloudflare R2 secret access key"),
                ("S3_ENDPOINT_URL", "s3_endpoint_url", "Cloudflare R2 endpoint URL"),
                ("CDN_BASE_URL", "cdn_base_url", "Public CDN URL for serving images"),
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
