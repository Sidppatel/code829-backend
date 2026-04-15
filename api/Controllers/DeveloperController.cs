using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.DTOs.Auth;
using Contracts.DTOs.Logs;
using Contracts.Enums;
using Db;
using Serilog;
using Stripe;
using Db.Repositories;
using Db.Repositories.StoredProcedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("developer")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperController(
    EventPlatformDbContext context,
    ISettingsService settingsService,
    IAppSettingRepository settingsRepo,
    IImageService imageService,
    IAdminUserProcedures adminUserProc,
    IEncryptionService encryptionService
) : ControllerBase
{
    /// <summary>
    /// Get paginated email logs.
    /// </summary>
    [HttpGet("email-log")]
    public async Task<IActionResult> GetEmailLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? recipient = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.EmailLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(recipient))
            query = query.Where(e => e.Recipient.Contains(recipient));

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EmailLogDto(e.Id, e.Recipient, e.Subject, e.Body, e.Status, e.Timestamp))
            .ToListAsync();

        return Ok(new PagedResponse<EmailLogDto>(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// Developer logs: errors, exceptions, stack traces. Filterable by severity, date, path.
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetDevLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? severity = null, [FromQuery] string? path = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.DeveloperLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<LogSeverity>(severity, true, out var sev))
            query = query.Where(l => l.Severity == sev);
        if (!string.IsNullOrWhiteSpace(path))
            query = query.Where(l => l.RequestPath != null && l.RequestPath.Contains(path));
        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new DeveloperLogDto(
                l.Id, l.Timestamp, l.Severity.ToString(), l.Message, l.ExceptionType,
                l.StackTrace, l.RequestPath, l.RequestMethod, l.StatusCode,
                l.UserId, l.IpAddress, l.CorrelationId, l.MetadataJson))
            .ToListAsync();

        return Ok(new PagedResponse<DeveloperLogDto>(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// System logs: complete audit trail with before/after JSON diffs.
    /// Supports cursor-based pagination via 'after' parameter (timestamp).
    /// </summary>
    [HttpGet("system-logs")]
    public async Task<IActionResult> GetSystemLogs(
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? after = null,
        [FromQuery] string? category = null,
        [FromQuery] string? entityType = null)
    {
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.SystemLogs.AsQueryable();

        if (after.HasValue)
            query = query.Where(l => l.Timestamp < after.Value);
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<LogCategory>(category, true, out var cat))
            query = query.Where(l => l.Category == cat);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(l => l.EntityType == entityType);

        var items = await query.OrderByDescending(l => l.Timestamp)
            .Take(pageSize + 1) // Fetch one extra to detect hasMore
            .Select(l => new SystemLogDto(
                l.Id, l.Timestamp, l.Category.ToString(), l.Action, l.Source,
                l.EntityType, l.EntityId, l.BeforeJson, l.AfterJson,
                l.ActorId, l.CorrelationId, l.DurationMs, l.MetadataJson))
            .ToListAsync();

        var hasMore = items.Count > pageSize;
        if (hasMore) items = items.Take(pageSize).ToList();

        var nextCursor = items.Count > 0 ? items[^1].Timestamp : (DateTime?)null;

        return Ok(new { items, hasMore, nextCursor });
    }

    /// <summary>
    /// Get all settings (values masked for display).
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var all = await settingsRepo.GetAllAsync();
        var dtos = new List<SettingDto>();
        foreach (var s in all)
        {
            var decrypted = await settingsService.GetOrDefaultAsync(s.Key) ?? "";
            var masked = decrypted.Length > 4
                ? new string('*', decrypted.Length - 4) + decrypted[^4..]
                : "****";
            dtos.Add(new SettingDto(s.Key, masked, s.Description, s.UpdatedAt));
        }

        return Ok(dtos);
    }

    /// <summary>
    /// Update a setting value.
    /// </summary>
    private static readonly HashSet<string> MutableSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "app_name", "default_platform_fee_open_cents", "default_platform_fee_grid_cents",
        // Email
        "resend_api_key", "email_from_address",
        // Stripe
        "stripe_secret_key", "stripe_publishable_key", "stripe_webhook_secret", "stripe_tax_enabled",
        // URLs (for deployment changes)
        "frontend_url", "cors_origins"
    };

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSetting([FromBody] UpdateSettingRequest request)
    {
        if (!MutableSettings.Contains(request.Key))
            return BadRequest(new ApiError(400, $"Setting '{request.Key}' cannot be modified via the API", HttpContext.TraceIdentifier));

        await settingsService.SetAsync(request.Key, request.Value);
        return Ok(new { message = $"Setting '{request.Key}' updated" });
    }

    /// <summary>
    /// Get Stripe integration status — verifies all keys by making a live API call.
    /// </summary>
    [HttpGet("stripe/status")]
    public async Task<IActionResult> GetStripeStatus()
    {
        var secretKey = await settingsService.GetOrDefaultAsync("stripe_secret_key") ?? "";
        var publishableKey = await settingsService.GetOrDefaultAsync("stripe_publishable_key") ?? "";
        var webhookSecret = await settingsService.GetOrDefaultAsync("stripe_webhook_secret") ?? "";
        var taxEnabled = (await settingsService.GetOrDefaultAsync("stripe_tax_enabled", "false")) == "true";

        var secretStatus = ClassifyKey(secretKey, "sk_");
        var publishableStatus = ClassifyKey(publishableKey, "pk_");
        var webhookStatus = ClassifyKey(webhookSecret, "whsec_");

        StripeAccountInfo? account = null;
        var verified = false;
        string? verificationError = null;

        // Verify by calling Stripe Balance API (lightweight) then fetch account info
        if (secretStatus.Configured)
        {
            try
            {
                var client = new StripeClient(secretKey);

                // Step 1: Verify key is valid via Balance API (no ID required)
                var balanceService = new BalanceService(client);
                await balanceService.GetAsync();

                // Step 2: Fetch account details via raw request to GET /v1/account
                var response = await client.RawRequestAsync(HttpMethod.Get, "/v1/account");
                var doc = System.Text.Json.JsonDocument.Parse(response.Content);
                var root = doc.RootElement;

                account = new StripeAccountInfo(
                    root.GetProperty("id").GetString()!,
                    root.TryGetProperty("business_profile", out var bp) && bp.TryGetProperty("name", out var name)
                        ? name.ValueKind == System.Text.Json.JsonValueKind.Null ? null : name.GetString()
                        : null,
                    root.TryGetProperty("country", out var country) ? country.GetString() : null,
                    root.TryGetProperty("charges_enabled", out var ce) && ce.GetBoolean(),
                    root.TryGetProperty("payouts_enabled", out var pe) && pe.GetBoolean(),
                    root.TryGetProperty("details_submitted", out var ds) && ds.GetBoolean());
                verified = true;

                Log.Information("[StripeStatus] Verified account {AccountId} ({Country})", account.Id, account.Country);
            }
            catch (StripeException ex)
            {
                verified = false;
                verificationError = ex.StripeError?.Message ?? ex.Message;
                Log.Warning("[StripeStatus] Verification failed: {Error}", verificationError);
            }
        }
        else
        {
            verificationError = secretKey == "MOCK_DEV"
                ? "Using mock payment service (development mode)"
                : "Stripe secret key not configured";
        }

        var dto = new StripeStatusDto(
            secretStatus, publishableStatus, webhookStatus,
            taxEnabled, verified, verificationError, account);

        return Ok(dto);
    }

    /// <summary>
    /// Update one or more Stripe keys at once.
    /// </summary>
    [HttpPut("stripe/keys")]
    public async Task<IActionResult> UpdateStripeKeys([FromBody] UpdateStripeKeysRequest request)
    {
        var updated = new List<string>();

        if (request.SecretKey is not null)
        {
            if (request.SecretKey != "MOCK_DEV" && !request.SecretKey.StartsWith("sk_"))
                return BadRequest(new ApiError(400, "Secret key must start with 'sk_test_' or 'sk_live_'", HttpContext.TraceIdentifier));
            await settingsService.SetAsync("stripe_secret_key", request.SecretKey);
            updated.Add("stripe_secret_key");
        }

        if (request.PublishableKey is not null)
        {
            if (request.PublishableKey != "MOCK_DEV" && !request.PublishableKey.StartsWith("pk_"))
                return BadRequest(new ApiError(400, "Publishable key must start with 'pk_test_' or 'pk_live_'", HttpContext.TraceIdentifier));
            await settingsService.SetAsync("stripe_publishable_key", request.PublishableKey);
            updated.Add("stripe_publishable_key");
        }

        if (request.WebhookSecret is not null)
        {
            if (request.WebhookSecret != "MOCK_DEV" && !request.WebhookSecret.StartsWith("whsec_"))
                return BadRequest(new ApiError(400, "Webhook secret must start with 'whsec_'", HttpContext.TraceIdentifier));
            await settingsService.SetAsync("stripe_webhook_secret", request.WebhookSecret);
            updated.Add("stripe_webhook_secret");
        }

        if (request.TaxEnabled is not null)
        {
            await settingsService.SetAsync("stripe_tax_enabled", request.TaxEnabled.Value ? "true" : "false");
            updated.Add("stripe_tax_enabled");
        }

        if (updated.Count == 0)
            return BadRequest(new ApiError(400, "No keys provided to update", HttpContext.TraceIdentifier));

        Log.Information("[StripeKeys] Updated: {Keys}", string.Join(", ", updated));
        return Ok(new { message = $"Updated {updated.Count} Stripe setting(s)", updated });
    }

    private static StripeKeyStatus ClassifyKey(string value, string expectedPrefix)
    {
        if (string.IsNullOrEmpty(value) || value == "MOCK_DEV")
        {
            return new StripeKeyStatus(
                Configured: false,
                Mode: value == "MOCK_DEV" ? "mock" : "not_set",
                Masked: value == "MOCK_DEV" ? "MOCK_DEV" : "");
        }

        var mode = value.Contains("_live_") ? "live"
                 : value.Contains("_test_") ? "test"
                 : value.StartsWith(expectedPrefix) ? "unknown"
                 : "invalid_format";

        var masked = value.Length > 8
            ? value[..7] + new string('*', value.Length - 11) + value[^4..]
            : new string('*', value.Length);

        return new StripeKeyStatus(Configured: true, Mode: mode, Masked: masked);
    }

    /// <summary>
    /// Get all users.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items = users, totalCount = totalCount, page, pageSize });
    }

    /// <summary>
    /// Get all admin users (paginated, searchable).
    /// </summary>
    [HttpGet("admin-users")]
    public async Task<IActionResult> GetAdminUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = context.AdminUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Email.ToLower().Contains(term) ||
                a.FirstName.ToLower().Contains(term) ||
                a.LastName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<AdminRole>(role, true, out var adminRole))
            query = query.Where(a => a.Role == adminRole);

        var totalCount = await query.CountAsync();

        var admins = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.FirstName, a.LastName, a.Email,
                Role = a.Role.ToString(),
                a.IsActive, a.CreatedAt, a.LastLoginAt, a.Phone
            })
            .ToListAsync();

        return Ok(new { items = admins, totalCount, page, pageSize });
    }

    /// <summary>
    /// Create a new admin user.
    /// </summary>
    [HttpPost("admin-users")]
    public async Task<IActionResult> CreateAdminUser([FromBody] CreateAdminUserRequest request)
    {
        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role. Must be Staff, Admin, or Developer", HttpContext.TraceIdentifier));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new ApiError(400, "Password must be at least 8 characters", HttpContext.TraceIdentifier));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await context.AdminUsers.AnyAsync(a => a.Email == normalizedEmail))
            return Conflict(new ApiError(409, "An admin user with this email already exists", HttpContext.TraceIdentifier));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var emailHash = encryptionService.HashEmail(normalizedEmail);

        var id = await adminUserProc.CreateAsync(
            normalizedEmail, emailHash, request.FirstName.Trim(), request.LastName.Trim(),
            passwordHash, role.ToString());

        return Created($"/developer/admin-users/{id}", new { id, message = $"{role} user created" });
    }

    /// <summary>
    /// Update an admin user (role, active status, profile).
    /// </summary>
    [HttpPut("admin-users/{id:guid}")]
    public async Task<IActionResult> UpdateAdminUser(Guid id, [FromBody] UpdateAdminUserRequest request)
    {
        var admin = await context.AdminUsers.FindAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));

        if (request.Role is not null && !Enum.TryParse<AdminRole>(request.Role, true, out _))
            return BadRequest(new ApiError(400, "Invalid role", HttpContext.TraceIdentifier));

        if (admin.Role == AdminRole.Developer && request.Role is not null && request.Role != "Developer")
            return BadRequest(new ApiError(400, "Cannot demote a Developer", HttpContext.TraceIdentifier));

        await adminUserProc.UpdateAsync(id,
            firstName: request.FirstName, lastName: request.LastName,
            phone: request.Phone, role: request.Role, isActive: request.IsActive);

        return Ok(new { message = "Admin user updated" });
    }

    /// <summary>
    /// Reset an admin user's password (developer privilege, no current password needed).
    /// </summary>
    [HttpPut("admin-users/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetAdminPassword(Guid id, [FromBody] ResetAdminPasswordRequest request)
    {
        var admin = await context.AdminUsers.FindAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new ApiError(400, "Password must be at least 8 characters", HttpContext.TraceIdentifier));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await adminUserProc.UpdatePasswordAsync(id, passwordHash);

        return Ok(new { message = "Password reset" });
    }

    /// <summary>
    /// Deactivate an admin user.
    /// </summary>
    [HttpDelete("admin-users/{id:guid}")]
    public async Task<IActionResult> DeactivateAdminUser(Guid id)
    {
        var admin = await context.AdminUsers.FindAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));

        if (admin.Role == AdminRole.Developer)
            return BadRequest(new ApiError(400, "Cannot deactivate a Developer", HttpContext.TraceIdentifier));

        await adminUserProc.UpdateAsync(id, isActive: false);
        return Ok(new { message = "Admin user deactivated" });
    }

    /// <summary>
    /// Upload or replace the company/platform logo. Developer only.
    /// </summary>
    [HttpPost("logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // Use a fixed entity ID for the platform logo so there's only ever one
        var platformEntityId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Delete old logo if any
        var existing = await imageService.GetByEntityAsync("platform", platformEntityId);
        foreach (var old in existing)
            await imageService.DeleteAsync(old.Id);

        var result = await imageService.UploadAsync(file.OpenReadStream(), file.FileName, "platform", platformEntityId, userId);
        return Ok(result);
    }

    /// <summary>
    /// Get the current company/platform logo.
    /// </summary>
    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo()
    {
        var platformEntityId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var images = await imageService.GetByEntityAsync("platform", platformEntityId);
        var logo = images.FirstOrDefault();
        if (logo is null) return NotFound(new ApiError(404, "No logo uploaded", HttpContext.TraceIdentifier));
        return Ok(logo);
    }
}
