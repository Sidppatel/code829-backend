using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.DTOs.Auth;
using Contracts.DTOs.Logs;
using Contracts.DTOs.Organizations;
using Contracts.Enums;
using Db;
using Serilog;
using Stripe;
using Db.Repositories;
using Db.Repositories.StoredProcedures;
using Db.Entities.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/developer")]
[Authorize]
[RequireRole(UserRole.Developer)]
public class DeveloperController(
    EventPlatformDbContext context,
    ISettingsService settingsService,
    IAppSettingRepository settingsRepo,
    ISecretsProvider secrets,
    IImageService imageService,
    IBusinessUserProcedures businessUserProc,
    IUserProcedures userProc,
    IEncryptionService encryptionService,
    IOrganizationService organizationService,
    IOrganizationProcedures organizationProc,
    IStripeConnectService stripeConnect
) : ControllerBase
{
    [HttpGet("email-log")]
    public async Task<IActionResult> GetEmailLogs(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
    [FromQuery] string? recipient = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var recipientParam = (object?)recipient ?? DBNull.Value;

        var totalCount = await context.Database
            .SqlQueryRaw<int>("SELECT sp_count_email_logs({0}) AS \"Value\"", recipientParam)
            .FirstAsync();

        var items = await context.EmailLogs
            .FromSqlRaw("SELECT * FROM sp_get_email_logs({0}, {1}, {2})",
                recipientParam, (page - 1) * pageSize, pageSize)
            .AsNoTracking()
            .Select(e => new EmailLogDto(e.Id, e.Recipient, e.Subject, e.Body, e.Status, e.Timestamp))
            .ToListAsync();

        return Ok(new PagedResponse<EmailLogDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetDevLogs(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
    [FromQuery] string? severity = null, [FromQuery] string? path = null,
    [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        string? normalizedSeverity = null;
        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<LogSeverity>(severity, true, out var sev))
            normalizedSeverity = sev.ToString();

        var severityParam = (object?)normalizedSeverity ?? DBNull.Value;
        var pathParam = (object?)path ?? DBNull.Value;
        var fromParam = (object?)from ?? DBNull.Value;
        var toParam = (object?)to ?? DBNull.Value;

        var totalCount = await context.Database
            .SqlQueryRaw<int>("SELECT sp_count_developer_logs({0}, {1}, {2}, {3}) AS \"Value\"",
                severityParam, pathParam, fromParam, toParam)
            .FirstAsync();

        var items = await context.DeveloperLogViews
            .FromSqlRaw("SELECT * FROM sp_get_developer_logs({0}, {1}, {2}, {3}, {4}, {5})",
                severityParam, pathParam, fromParam, toParam, (page - 1) * pageSize, pageSize)
            .AsNoTracking()
            .Select(l => new DeveloperLogDto(
                l.Id, l.Timestamp, l.Severity, l.Message, l.ExceptionType,
                l.StackTrace, l.RequestPath, l.RequestMethod, l.StatusCode,
                l.BusinessUserId, l.IpAddress, l.CorrelationId, l.MetadataJson))
            .ToListAsync();

        return Ok(new PagedResponse<DeveloperLogDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("system-logs")]
    public async Task<IActionResult> GetSystemLogs(
    [FromQuery] int pageSize = 20,
    [FromQuery] DateTime? after = null,
    [FromQuery] string? category = null,
    [FromQuery] string? entityType = null)
    {
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        string? normalizedCategory = null;
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<LogCategory>(category, true, out var cat))
            normalizedCategory = cat.ToString();

        var afterParam = (object?)after ?? DBNull.Value;
        var categoryParam = (object?)normalizedCategory ?? DBNull.Value;
        var entityTypeParam = (object?)entityType ?? DBNull.Value;

        var items = await context.SystemLogViews
            .FromSqlRaw("SELECT * FROM sp_get_system_logs({0}, {1}, {2}, {3})",
                afterParam, categoryParam, entityTypeParam, pageSize + 1)
            .AsNoTracking()
            .Select(l => new SystemLogDto(
                l.Id, l.Timestamp, l.Category, l.Action, l.Source,
                l.EntityType, l.EntityId, l.BeforeJson, l.AfterJson,
                l.UserId, l.UserEmail, l.UserRole, l.CorrelationId, l.DurationMs, l.MetadataJson))
            .ToListAsync();

        var hasMore = items.Count > pageSize;
        if (hasMore) items = items.Take(pageSize).ToList();

        var nextCursor = items.Count > 0 ? items[^1].Timestamp : (DateTime?)null;

        return Ok(new { items, hasMore, nextCursor });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var all = await settingsRepo.GetAllAsync();
        var settingDtos = all.Select(s =>
            new SettingDto(s.Key, s.Value, s.Description, s.UpdatedAt)).ToList();

        var secretDtos = new List<SecretStatusDto>
        {
            new("JWT_SECRET", !string.IsNullOrEmpty(secrets.JwtSecret), "JWT signing secret"),
            new("STRIPE_SECRET_KEY", !string.IsNullOrEmpty(secrets.StripeSecretKey), "Stripe secret key"),
            new("STRIPE_PUBLISHABLE_KEY", !string.IsNullOrEmpty(secrets.StripePublishableKey), "Stripe publishable key"),
            new("STRIPE_WEBHOOK_SECRET", !string.IsNullOrEmpty(secrets.StripeWebhookSecret), "Stripe webhook signing secret"),
            new("RESEND_API_KEY", !string.IsNullOrEmpty(secrets.ResendApiKey), "Resend API key for sending emails"),
            new("S3_ACCESS_KEY", !string.IsNullOrEmpty(secrets.S3AccessKey), "Cloudflare R2 access key ID"),
            new("S3_SECRET_KEY", !string.IsNullOrEmpty(secrets.S3SecretKey), "Cloudflare R2 secret access key"),
            new("S3_BUCKET", !string.IsNullOrEmpty(secrets.S3Bucket), "Cloudflare R2 bucket name"),
            new("S3_ENDPOINT_URL", !string.IsNullOrEmpty(secrets.S3EndpointUrl), "Cloudflare R2 endpoint URL"),
            new("CDN_BASE_URL", !string.IsNullOrEmpty(secrets.CdnBaseUrl), "Public CDN URL for serving images"),
        };

        return Ok(new SettingsResponse(settingDtos, secretDtos));
    }

    private static readonly HashSet<string> MutableSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "app_name", "default_platform_fee_open_cents", "default_platform_fee_grid_cents",
        "email_from_address", "stripe_tax_enabled",
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

    [HttpGet("stripe/status")]
    public async Task<IActionResult> GetStripeStatus()
    {
        var secretKey = secrets.StripeSecretKey;
        var publishableKey = secrets.StripePublishableKey;
        var webhookSecret = secrets.StripeWebhookSecret;
        var taxEnabled = (await settingsService.GetOrDefaultAsync("stripe_tax_enabled", "false")) == "true";

        var secretStatus = ClassifyKey(secretKey, "sk_");
        var publishableStatus = ClassifyKey(publishableKey, "pk_");
        var webhookStatus = ClassifyKey(webhookSecret, "whsec_");

        StripeAccountInfo? account = null;
        var verified = false;
        string? verificationError = null;

        if (secretStatus.Configured)
        {
            try
            {
                var client = new StripeClient(secretKey);

                var balanceService = new BalanceService(client);
                await balanceService.GetAsync();

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

                Log.Information("[StripeStatus] Verified account {AccountId} ({Country})", account.StripeAccountId, account.Country);
            }
            catch (StripeException ex)
            {
                verified = false;
                var stripeErrorDetail = ex.StripeError?.Message ?? ex.Message;
                Log.Warning("[StripeStatus] Verification failed: {Error}", stripeErrorDetail);
                verificationError = "Payment processing failed";
            }
        }
        else
        {
            verificationError = "Stripe secret key not configured — set STRIPE_SECRET_KEY environment variable";
        }

        var dto = new StripeStatusDto(
            secretStatus, publishableStatus, webhookStatus,
            taxEnabled, verified, verificationError, account);

        return Ok(dto);
    }

    [HttpPut("stripe/keys")]
    public async Task<IActionResult> UpdateStripeKeys([FromBody] UpdateStripeKeysRequest request)
    {
        if (request.SecretKey is not null || request.PublishableKey is not null || request.WebhookSecret is not null)
            return BadRequest(new ApiError(400,
                "Stripe keys are now managed via environment variables (STRIPE_SECRET_KEY, STRIPE_PUBLISHABLE_KEY, STRIPE_WEBHOOK_SECRET). " +
                "Update them in your deployment environment and restart the application.",
                HttpContext.TraceIdentifier));

        if (request.TaxEnabled is not null)
        {
            await settingsService.SetAsync("stripe_tax_enabled", request.TaxEnabled.Value ? "true" : "false");
            Log.Information("[StripeKeys] Updated stripe_tax_enabled to {Value}", request.TaxEnabled.Value);
            return Ok(new { message = "Updated stripe_tax_enabled", updated = new[] { "stripe_tax_enabled" } });
        }

        return BadRequest(new ApiError(400, "No settings provided to update", HttpContext.TraceIdentifier));
    }

    private static StripeKeyStatus ClassifyKey(string value, string expectedPrefix)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new StripeKeyStatus(
                Configured: false,
                Mode: "not_set",
                Masked: "");
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

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? search = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = context.UserProfileViews.AsNoTracking();

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
                u.UserId,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.IsActive,
                u.LastLoginAt,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items = users, totalCount = totalCount, page, pageSize });
    }

    [HttpPut("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] bool isActive)
    {
        var updated = await userProc.SetUserActiveAsync(id, isActive);
        if (!updated) return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        Log.Information("[Developer] User {UserId} status updated to {Status}", id, isActive);
        return Ok(new { message = "User status updated" });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var deleted = await userProc.DeleteUserAsync(id);
        if (!deleted) return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        Log.Information("[Developer] User {UserId} deleted", id);
        return Ok(new { message = "User deleted successfully" });
    }

    [HttpGet("admin-users")]
    public async Task<IActionResult> GetAdminUsers(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? search = null,
    [FromQuery] string? role = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = context.BusinessUserViews.AsNoTracking();

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
                a.BusinessUserId,
                a.FirstName,
                a.LastName,
                a.Email,
                Role = a.Role.ToString(),
                a.IsActive,
                a.CreatedAt,
                a.LastLoginAt,
                a.Phone
            })
            .ToListAsync();

        return Ok(new { items = admins, totalCount, page, pageSize });
    }

    [HttpPost("admin-users")]
    public async Task<IActionResult> CreateAdminUser([FromBody] CreateBusinessUserRequest request)
    {
        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role. Must be Staff, Admin, or Developer", HttpContext.TraceIdentifier));

        var (pwValid, pwError) = Api.Helpers.PasswordValidator.Validate(request.Password);
        if (!pwValid)
            return BadRequest(new ApiError(400, pwError!, HttpContext.TraceIdentifier));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await businessUserProc.ExistsByEmailAsync(normalizedEmail))
            return Conflict(new ApiError(409, "An admin user with this email already exists", HttpContext.TraceIdentifier));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var emailHash = encryptionService.HashEmail(normalizedEmail);

        var id = await businessUserProc.CreateAsync(
            normalizedEmail, emailHash, request.FirstName.Trim(), request.LastName.Trim(),
            passwordHash, role.ToString());

        return Created($"/developer/admin-users/{id}", new { id, message = $"{role} user created" });
    }

    [HttpPut("admin-users/{id:guid}")]
    public async Task<IActionResult> UpdateAdminUser(Guid id, [FromBody] UpdateBusinessUserRequest request)
    {
        var admin = await businessUserProc.GetByIdAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));

        if (request.Role is not null && !Enum.TryParse<AdminRole>(request.Role, true, out _))
            return BadRequest(new ApiError(400, "Invalid role", HttpContext.TraceIdentifier));

        if (admin.Role == AdminRole.Developer && request.Role is not null && request.Role != "Developer")
            return BadRequest(new ApiError(400, "Cannot demote a Developer", HttpContext.TraceIdentifier));

        await businessUserProc.UpdateAsync(id,
            firstName: request.FirstName, lastName: request.LastName,
            phone: request.Phone, role: request.Role, isActive: request.IsActive);

        return Ok(new { message = "Admin user updated" });
    }

    [HttpPut("admin-users/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetAdminPassword(Guid id, [FromBody] ResetBusinessUserPasswordRequest request)
    {
        var admin = await businessUserProc.GetByIdAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));

        var (pwValid2, pwError2) = Api.Helpers.PasswordValidator.Validate(request.NewPassword);
        if (!pwValid2)
            return BadRequest(new ApiError(400, pwError2!, HttpContext.TraceIdentifier));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await businessUserProc.UpdatePasswordAsync(id, passwordHash);

        return Ok(new { message = "Password reset" });
    }

    [HttpDelete("admin-users/{id:guid}")]
    public async Task<IActionResult> DeactivateAdminUser(Guid id)
    {
        var admin = await businessUserProc.GetByIdAsync(id);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));

        if (admin.Role == AdminRole.Developer)
            return BadRequest(new ApiError(400, "Cannot deactivate a Developer", HttpContext.TraceIdentifier));

        await businessUserProc.UpdateAsync(id, isActive: false);
        return Ok(new { message = "Admin user deactivated" });
    }

    [HttpPost("logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        var (valid, error) = Api.Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var platformEntityId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var existing = await imageService.GetByEntityAsync("platform", platformEntityId);
        foreach (var old in existing)
            await imageService.DeleteAsync(old.ImageId);

        var result = await imageService.UploadAsync(file.OpenReadStream(), file.FileName, "platform", platformEntityId, userId, uploaderType: "admin");
        return Ok(result);
    }

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

    [HttpGet("organizations")]
    public async Task<IActionResult> ListOrganizations(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? search = null,
    [FromQuery] bool includeArchived = false)
    {
        var result = await organizationService.ListAsync(search, page, pageSize, includeArchived);
        return Ok(result);
    }

    [HttpPost("organizations")]
    public async Task<IActionResult> CreateOrganization([FromBody] OrganizationCreateRequest request)
    {
        var countryCode = string.IsNullOrEmpty(request.CountryCode) ? "US" : request.CountryCode.ToUpperInvariant();

        if (request.InitialMemberBusinessUserId is { } memberId)
        {
            var member = await businessUserProc.GetByIdAsync(memberId);
            if (member is null)
                return BadRequest(new ApiError(400,
                    $"BusinessUser {memberId} not found — cannot attach as initial member",
                    HttpContext.TraceIdentifier));
        }

        var id = await organizationService.CreateAsync(
            request.Name.Trim(), request.LegalName?.Trim(), countryCode,
            request.InitialMemberBusinessUserId);

        var dto = await organizationService.GetAsync(id);
        return Created($"/v1/developer/organizations/{id}", dto);
    }

    [HttpGet("organizations/{id:guid}")]
    public async Task<IActionResult> GetOrganization(Guid id)
    {
        var org = await organizationService.GetDetailAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));
        return Ok(org);
    }

    [HttpPut("organizations/{id:guid}")]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] OrganizationUpdateRequest request)
    {
        var existing = await organizationService.GetAsync(id);
        if (existing is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        var normalized = request with
        {
            Name = request.Name?.Trim(),
            LegalName = request.LegalName?.Trim(),
            CountryCode = request.CountryCode?.ToUpperInvariant()
        };

        await organizationService.UpdateAsync(id, normalized);
        var updated = await organizationService.GetDetailAsync(id);
        return Ok(updated);
    }

    [HttpPost("organizations/{id:guid}/members")]
    public async Task<IActionResult> AddOrganizationMember(Guid id, [FromBody] OrganizationMemberRequest request)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        var member = await businessUserProc.GetByIdAsync(request.BusinessUserId);
        if (member is null)
            return NotFound(new ApiError(404, $"BusinessUser {request.BusinessUserId} not found", HttpContext.TraceIdentifier));

        try
        {
            await organizationService.AddMemberAsync(id, request.BusinessUserId);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "22023")
        {
            return BadRequest(new ApiError(400, ex.MessageText, HttpContext.TraceIdentifier));
        }

        var updated = await organizationService.GetDetailAsync(id);
        return Ok(updated);
    }

    [HttpDelete("organizations/{id:guid}/members/{businessUserId:guid}")]
    public async Task<IActionResult> RemoveOrganizationMember(Guid id, Guid businessUserId)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        try
        {
            await organizationService.RemoveMemberAsync(id, businessUserId);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23503")
        {
            return Conflict(new ApiError(409, ex.MessageText, HttpContext.TraceIdentifier));
        }

        var updated = await organizationService.GetDetailAsync(id);
        return Ok(updated);
    }

    [HttpPost("organizations/{id:guid}/stripe-account")]
    public async Task<IActionResult> CreateStripeAccount(Guid id, [FromBody] StartStripeOnboardingRequest? request = null)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        var stripeAccountId = org.StripeConnectedAccountId;

        if (string.IsNullOrEmpty(stripeAccountId))
        {

            var devEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? "platform@code829.com";

            var businessType = string.IsNullOrWhiteSpace(request?.BusinessType)
                ? "individual"
                : request.BusinessType;
            var legalName = string.IsNullOrWhiteSpace(request?.LegalName)
                ? (org.LegalName ?? org.Name)
                : request.LegalName;
            var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
            var productDescription = string.IsNullOrWhiteSpace(request?.ProductDescription)
                ? $"Event tickets and admissions sold via the {appName} platform on behalf of {legalName}."
                : request.ProductDescription;
            var mcc = string.IsNullOrWhiteSpace(request?.Mcc) ? "7922" : request.Mcc;

            var prefill = new StripeBusinessProfilePrefill(
                LegalName: legalName,
                ProductDescription: productDescription,
                Mcc: mcc,
                BusinessType: businessType);

            try
            {
                stripeAccountId = await stripeConnect.CreateExpressAccountAsync(id, devEmail, org.CountryCode, prefill);
                await organizationProc.UpdateStripeAccountAsync(id, stripeAccountId);
            }
            catch (StripeException ex)
            {
                Log.Error(ex, "[Developer] Stripe Connect account creation failed for org {OrganizationId}", id);
                return StatusCode(502, new ApiError(502,
                    "Failed to create Stripe Connect account — see server logs", HttpContext.TraceIdentifier));
            }
        }

        try
        {
            var url = await stripeConnect.CreateOnboardingLinkAsync(stripeAccountId, OnboardingLinkScope.Identity);

            var expiresAt = DateTime.UtcNow.AddMinutes(5);
            return Created($"/v1/developer/organizations/{id}/stripe-account",
                new StripeOnboardingLinkResponse(url, expiresAt));
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Developer] Stripe onboarding link creation failed for org {OrganizationId}", id);
            return StatusCode(502, new ApiError(502,
                "Failed to create Stripe onboarding link", HttpContext.TraceIdentifier));
        }
    }

    [HttpDelete("organizations/{id:guid}/stripe-account")]
    public async Task<IActionResult> ClearStripeAccount(Guid id)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        try
        {
            var detail = await organizationService.ClearStripeAccountAsync(id);
            if (detail is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));
            return Ok(detail);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Developer] Stripe account delete failed for org {OrganizationId}", id);
            return StatusCode(502, new ApiError(502,
                "Failed to delete Stripe Connect account — see server logs", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("organizations/{id:guid}/stripe-onboarding-email")]
    public async Task<IActionResult> SendStripeOnboardingEmail(
    Guid id, [FromBody] StripeOnboardingEmailRequest request)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        try
        {
            var response = await organizationService.SendOnboardingLinkEmailAsync(
                id, request.BusinessUserId, request.RecipientEmail);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier));
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Developer] Stripe onboarding link creation failed for org {OrganizationId}", id);
            return StatusCode(502, new ApiError(502, "Failed to create Stripe onboarding link", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("organizations/{id:guid}/stripe-onboarding-link")]
    public async Task<IActionResult> CreateStripeOnboardingLink(Guid id, [FromBody] StripeOnboardingLinkRequest request)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        if (string.IsNullOrEmpty(org.StripeConnectedAccountId))
            return BadRequest(new ApiError(400,
                "Organization has no Stripe account yet — call POST /stripe-account first",
                HttpContext.TraceIdentifier));

        var scope = string.Equals(request.Scope, "bank", StringComparison.OrdinalIgnoreCase)
            ? OnboardingLinkScope.BankOnly
            : OnboardingLinkScope.Identity;

        try
        {
            var url = await stripeConnect.CreateOnboardingLinkAsync(org.StripeConnectedAccountId, scope);
            return Ok(new StripeOnboardingLinkResponse(url, DateTime.UtcNow.AddMinutes(5)));
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Developer] Stripe onboarding link creation failed for org {OrganizationId}", id);
            return StatusCode(502, new ApiError(502, "Failed to create Stripe onboarding link", HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("organizations/{id:guid}/stripe-status")]
    public async Task<IActionResult> GetStripeStatus(Guid id)
    {
        var org = await organizationService.GetAsync(id);
        if (org is null) return NotFound(new ApiError(404, "Organization not found", HttpContext.TraceIdentifier));

        if (string.IsNullOrEmpty(org.StripeConnectedAccountId))
        {

            return Ok(new OrganizationStripeStatusDto(
                OrganizationId: org.Id,
                OrganizationName: org.Name,
                StripeAccount: null,
                State: OrganizationStripeStateMapper.Derive(
                    stripeAccountId: null,
                    detailsSubmitted: false,
                    chargesEnabled: false,
                    payoutsEnabled: false,
                    disabledReason: null),
                BankAccountLast4: null,

                Members: new List<OrganizationMemberDto>(),
                ExpressDashboardUrl: null,
                FetchedAt: DateTime.UtcNow));
        }

        try
        {
            var status = await stripeConnect.FetchAccountStatusAsync(org.StripeConnectedAccountId);

            var requirementsJson = System.Text.Json.JsonSerializer.Serialize(status.RequirementsCurrentlyDue);
            await organizationProc.UpdateStripeStatusAsync(
                status.AccountId,
                status.ChargesEnabled, status.PayoutsEnabled,
                status.DetailsSubmitted, requirementsJson);

            var state = OrganizationStripeStateMapper.Derive(
                stripeAccountId: status.AccountId,
                detailsSubmitted: status.DetailsSubmitted,
                chargesEnabled: status.ChargesEnabled,
                payoutsEnabled: status.PayoutsEnabled,
                disabledReason: status.DisabledReason);

            var expressDashboardUrl = status.PayoutsEnabled
                ? await stripeConnect.CreateLoginLinkAsync(org.StripeConnectedAccountId)
                : null;

            var stripeAccountDto = new StripeAccountStatusDto(
                AccountId: status.AccountId,
                ChargesEnabled: status.ChargesEnabled,
                PayoutsEnabled: status.PayoutsEnabled,
                DetailsSubmitted: status.DetailsSubmitted,
                RequirementsCurrentlyDue: status.RequirementsCurrentlyDue);

            return Ok(new OrganizationStripeStatusDto(
                OrganizationId: org.Id,
                OrganizationName: org.Name,
                StripeAccount: stripeAccountDto,
                State: state,
                BankAccountLast4: status.BankAccountLast4,

                Members: new List<OrganizationMemberDto>(),
                ExpressDashboardUrl: expressDashboardUrl,
                FetchedAt: DateTime.UtcNow));
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[Developer] Stripe status fetch failed for org {OrganizationId}", id);
            return StatusCode(502, new ApiError(502, "Failed to fetch Stripe account status", HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("visits")]
    public async Task<IActionResult> GetVisits(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? portal = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = context.SiteVisitViews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(portal))
        {
            query = query.Where(v => v.Portal == portal);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v =>
                v.Path.ToLower().Contains(term) ||
                (v.IpAddress != null && v.IpAddress.ToLower().Contains(term)) ||
                (v.UserEmail != null && v.UserEmail.ToLower().Contains(term)) ||
                (v.UserFullName != null && v.UserFullName.ToLower().Contains(term)) ||
                (v.Browser != null && v.Browser.ToLower().Contains(term)) ||
                (v.Os != null && v.Os.ToLower().Contains(term))
            );
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new SiteVisitDto(
                v.Id, v.Timestamp, v.Path, v.IpAddress, v.UserAgent, v.Referrer,
                v.ScreenResolution, v.Portal, v.Browser, v.Os, v.UserId, v.BusinessUserId,
                v.UserEmail, v.UserFullName, v.UserRole
            ))
            .ToListAsync();

        return Ok(new PagedResponse<SiteVisitDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("visits/stats")]
    public async Task<IActionResult> GetVisitsStats(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var thirtyDaysAgo = today.AddDays(-30);

        var baseQuery = context.SiteVisitViews.AsNoTracking();

        var totalPageViews = await baseQuery.CountAsync(v => v.Timestamp >= thirtyDaysAgo, ct);
        var uniqueVisitors = await baseQuery
            .Where(v => v.Timestamp >= thirtyDaysAgo)
            .Select(v => v.IpAddress)
            .Distinct()
            .CountAsync(ct);

        var pageViewsToday = await baseQuery.CountAsync(v => v.Timestamp >= today, ct);
        var pageViewsYesterday = await baseQuery.CountAsync(v => v.Timestamp >= yesterday && v.Timestamp < today, ct);

        var visitsByDateRaw = await baseQuery
            .Where(v => v.Timestamp >= thirtyDaysAgo)
            .GroupBy(v => v.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(g => g.Date)
            .ToListAsync(ct);

        var visitsByDate = visitsByDateRaw
            .Select(g => new VisitorChartPointDto(g.Date.ToString("yyyy-MM-dd"), g.Count))
            .ToList();

        var visitsByBrowserRaw = await baseQuery
            .Where(v => v.Timestamp >= thirtyDaysAgo)
            .GroupBy(v => v.Browser)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        var visitsByBrowser = visitsByBrowserRaw
            .Select(x => new VisitorStatItemDto(x.Name ?? "Unknown", x.Count))
            .ToList();

        var visitsByPortalRaw = await baseQuery
            .Where(v => v.Timestamp >= thirtyDaysAgo)
            .GroupBy(v => v.Portal)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var visitsByPortal = visitsByPortalRaw
            .Select(x => new VisitorStatItemDto(x.Name ?? "Unknown", x.Count))
            .ToList();

        var visitsByOsRaw = await baseQuery
            .Where(v => v.Timestamp >= thirtyDaysAgo)
            .GroupBy(v => v.Os)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        var visitsByOs = visitsByOsRaw
            .Select(x => new VisitorStatItemDto(x.Name ?? "Unknown", x.Count))
            .ToList();

        return Ok(new VisitorStatsDto(
            totalPageViews,
            uniqueVisitors,
            pageViewsToday,
            pageViewsYesterday,
            visitsByDate,
            visitsByBrowser,
            visitsByPortal,
            visitsByOs
        ));
    }
}

public record SiteVisitDto(
    Guid Id,
    DateTime Timestamp,
    string Path,
    string? IpAddress,
    string? UserAgent,
    string? Referrer,
    string? ScreenResolution,
    string? Portal,
    string? Browser,
    string? Os,
    Guid? UserId,
    Guid? BusinessUserId,
    string? UserEmail,
    string? UserFullName,
    string? UserRole
);

public record VisitorChartPointDto(string Date, int Count);
public record VisitorStatItemDto(string Name, int Count);
public record VisitorStatsDto(
    int TotalPageViews,
    int UniqueVisitors,
    int PageViewsToday,
    int PageViewsYesterday,
    List<VisitorChartPointDto> VisitsByDate,
    List<VisitorStatItemDto> VisitsByBrowser,
    List<VisitorStatItemDto> VisitsByPortal,
    List<VisitorStatItemDto> VisitsByOs
);
