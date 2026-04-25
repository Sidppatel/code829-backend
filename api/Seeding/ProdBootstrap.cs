using Api.Helpers;
using Api.Services;
using Contracts.Enums;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Seeding;

/// <summary>
/// One-shot production bootstrap. Triggered by RUN_PROD_BOOTSTRAP=true.
/// Idempotent — each section checks for existence before writing.
/// The caller (Program.cs) runs this and exits the process, so the web
/// server never starts on a bootstrap boot.
/// </summary>
public static class ProdBootstrap
{
    public static async Task<bool> RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<EventPlatformDbContext>();
        var encryption = sp.GetRequiredService<IEncryptionService>();
        var settings = sp.GetRequiredService<ISettingsService>();
        var businessUserProc = sp.GetRequiredService<IBusinessUserProcedures>();

        Log.Information("[ProdBootstrap] Starting");

        await db.Database.MigrateAsync();
        Log.Information("[ProdBootstrap] Migrations applied");

        var settingsAdded = await EnsureDefaultSettingsAsync(settings);
        Log.Information("[ProdBootstrap] Settings: {Added} added, rest already present", settingsAdded);

        var developerAdded = await EnsureInitialDeveloperAsync(db, businessUserProc, encryption);
        Log.Information("[ProdBootstrap] Initial developer: {State}", developerAdded ? "created" : "already present");

        Log.Information("[ProdBootstrap] Complete");
        return settingsAdded > 0 || developerAdded;
    }

    private static async Task<int> EnsureDefaultSettingsAsync(ISettingsService settings)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["magic_link_expiry_minutes"] = ("15", "Magic link token lifetime in minutes"),
            ["hold_expiry_minutes"] = ("10", "Seat hold duration in minutes"),
            ["email_from_address"] = (Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS") ?? "noreply@code829.com", "Sender email address"),
            ["app_name"] = ("Code829", "Application name used in emails and SEO"),
            ["default_platform_fee_open_cents"] = ("1000", "Default platform fee for Open events in cents"),
            ["default_platform_fee_grid_cents"] = ("2500", "Default platform fee for Grid events in cents"),
            ["stripe_tax_enabled"] = ("true", "Enable Stripe Tax for automatic tax calculation"),
            ["connect_enforcement_enabled"] = ("false", "When true, every purchase requires the organizer's Organization to have a fully onboarded Stripe Connect account"),
            ["frontend_url"] = (Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://code829.com", "Frontend URL for magic link emails"),
            ["cors_origins"] = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "https://code829.com", "Comma-separated allowed CORS origins"),
            ["search_results_per_page"] = ("20", "Search pagination page size"),
            ["dev_log_retention_days"] = ("90", "Developer log retention in days"),
            ["admin_log_retention_days"] = ("365", "Admin log retention in days"),
            ["system_log_retention_days"] = ("30", "System log retention in days"),
            ["s3_region"] = ("auto", "Cloudflare R2 region"),
            ["rate_limit_disabled"] = ("false", "Set to 'true' to bypass all rate limits (testing only)"),
        };

        var added = 0;
        foreach (var (key, (value, description)) in defaults)
        {
            var existing = await settings.GetOrDefaultAsync(key);
            if (existing is null)
            {
                await settings.SetAsync(key, value, description);
                added++;
            }
        }
        return added;
    }

    private static async Task<bool> EnsureInitialDeveloperAsync(
        EventPlatformDbContext db,
        IBusinessUserProcedures businessUserProc,
        IEncryptionService encryption)
    {
        // Existence check covers re-runs — any BusinessUser row means bootstrap already ran.
        if (await db.BusinessUsers.AnyAsync())
            return false;

        var email = Environment.GetEnvironmentVariable("BOOTSTRAP_DEVELOPER_EMAIL")
            ?? throw new InvalidOperationException("BOOTSTRAP_DEVELOPER_EMAIL must be set for prod bootstrap");
        var password = Environment.GetEnvironmentVariable("BOOTSTRAP_DEVELOPER_PASSWORD")
            ?? throw new InvalidOperationException("BOOTSTRAP_DEVELOPER_PASSWORD must be set for prod bootstrap");
        var firstName = Environment.GetEnvironmentVariable("BOOTSTRAP_DEVELOPER_FIRST_NAME") ?? "Platform";
        var lastName = Environment.GetEnvironmentVariable("BOOTSTRAP_DEVELOPER_LAST_NAME") ?? "Owner";

        var (pwValid, pwError) = PasswordValidator.Validate(password);
        if (!pwValid)
            throw new InvalidOperationException($"BOOTSTRAP_DEVELOPER_PASSWORD rejected: {pwError}");

        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        await businessUserProc.CreateAsync(
            email,
            encryption.HashEmail(email),
            firstName,
            lastName,
            hash,
            AdminRole.Developer.ToString());

        Log.Information("[ProdBootstrap] Created initial developer: {Email}", email);
        return true;
    }
}
