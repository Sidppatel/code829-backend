using Api.Services;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// Seeds initial admin users, regular users, app settings, and table templates on first run.
/// Each seeder short-circuits if its target table is non-empty, so this is a fresh-DB-only path.
///
/// Admin role semantics:
///   developer@code829.local  (Developer) — platform owner; IsOwnerOrDeveloper lets this
///                                          account edit ANY event regardless of OrganizerId
///   organizer@code829.local  (Admin)     — the event organizer; OWNS all seeded events
///   admin@code829.local      (Admin)     — platform staff; owns no events by design
///   staff@code829.local      (Staff)     — limited role; read-mostly
///
/// Single-owner model intentionally: splitting events across two Admin accounts made
/// testing brittle — logging in as the wrong one surfaced 403s that looked like bugs.
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
        var businessUserProc = scope.ServiceProvider.GetRequiredService<IBusinessUserProcedures>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        await SeedAdminUsersAsync(context, encryption, businessUserProc, env);
        await SeedUsersAsync(context, encryption, authProc);
        await SeedSettingsAsync(settingsService);
        await SeedTableTemplatesAsync(context);
    }

    private static async Task SeedAdminUsersAsync(EventPlatformDbContext context, IEncryptionService encryption, IBusinessUserProcedures businessUserProc, IWebHostEnvironment env)
    {
        // Defense-in-depth: seeded admin accounts carry known default passwords
        // ("Dev@12345", "Admin@12345", "Staff@12345"). Refuse to run anywhere but
        // Development so a misconfigured SEED_DATA flag cannot plant them in prod.
        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "SeedAdminUsersAsync refuses to run outside Development. " +
                "Default admin accounts contain known passwords — use ProdBootstrap for real deployments.");

        if (await context.BusinessUsers.AnyAsync())
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
            await businessUserProc.CreateAsync(email, encryption.HashEmail(email), firstName, lastName, hash, role.ToString());
        }

        Log.Information("[Seed] Created {Count} business users via SP", admins.Length);
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
        // Only non-sensitive runtime config is stored in the database.
        // Secrets (JWT, Stripe, Resend, S3) are now in environment variables via ISecretsProvider.
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["magic_link_expiry_minutes"] = ("15", "Magic link token lifetime in minutes"),
            ["hold_expiry_minutes"] = ("10", "Seat hold duration in minutes"),
            ["email_from_address"] = ("noreply@code829.local", "Sender email address"),
            ["app_name"] = ("Code829", "Application name used in emails and SEO"),
            ["default_platform_fee_open_cents"] = ("1000", "Default platform fee for Open events in cents ($10.00)"),
            ["default_platform_fee_grid_cents"] = ("2500", "Default platform fee for Grid events in cents ($25.00)"),
            ["stripe_tax_enabled"] = ("true", "Enable Stripe Tax for automatic tax calculation"),
            ["frontend_url"] = ("http://localhost:5173", "Frontend URL for magic link emails"),
            ["cors_origins"] = ("http://localhost:5173", "Comma-separated allowed CORS origins"),
            ["search_results_per_page"] = ("20", "Search pagination page size"),
            ["dev_log_retention_days"] = ("90", "Developer log retention in days"),
            ["admin_log_retention_days"] = ("365", "Admin log retention in days"),
            ["system_log_retention_days"] = ("30", "System log retention in days"),
            ["s3_region"] = ("auto", "Cloudflare R2 region (always 'auto')"),
            ["rate_limit_disabled"] = ("false", "Set to 'true' to bypass all rate limits (testing only)"),
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
        if (env is not null && env != "Development")
        {
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://code829.com";
            var current = await settings.GetOrDefaultAsync("frontend_url");
            if (current != frontendUrl)
            {
                await settings.SetAsync("frontend_url", frontendUrl, "Frontend URL for magic link emails");
                Log.Information("[Seed] Updated frontend_url to {Url}", frontendUrl);
            }

            var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "https://code829.com";
            var currentCors = await settings.GetOrDefaultAsync("cors_origins");
            if (currentCors != corsOrigins)
            {
                await settings.SetAsync("cors_origins", corsOrigins, "Comma-separated allowed CORS origins");
                Log.Information("[Seed] Updated cors_origins to {Origins}", corsOrigins);
            }

            var fromEmail = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS");
            if (!string.IsNullOrEmpty(fromEmail))
            {
                var currentFrom = await settings.GetOrDefaultAsync("email_from_address");
                if (currentFrom != fromEmail)
                {
                    await settings.SetAsync("email_from_address", fromEmail, "Sender email address");
                    Log.Information("[Seed] Updated email_from_address to {Email}", fromEmail);
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
