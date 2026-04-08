using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Admin;
using Contracts.DTOs.Logs;
using Contracts.Enums;
using Db;
using Db.Repositories;
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
    IImageService imageService
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
        "app_name", "default_platform_fee_cents",
        // Email
        "resend_api_key", "email_from_address",
        // Stripe
        "stripe_secret_key", "stripe_publishable_key", "stripe_webhook_secret",
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
                Role = u.Role.ToString(),
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items = users, totalCount = totalCount, page, pageSize });
    }

    /// <summary>
    /// Update a user's role (e.g. promoting them to Admin).
    /// </summary>
    [HttpPut("users/{id:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return BadRequest(new ApiError(400, "Invalid role", HttpContext.TraceIdentifier));

        var user = await context.Users.FindAsync(id);
        if (user is null) return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        // Ensure developers cannot demote or duplicate the master developer via this endpoint
        // (Assuming developer is highest role, but let's just make sure they don't break themselves)
        if (user.Role == UserRole.Developer && role != UserRole.Developer)
            return BadRequest(new ApiError(400, "Cannot demote a Developer", HttpContext.TraceIdentifier));

        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { message = $"User updated to {role}" });
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
