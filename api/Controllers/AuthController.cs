using Contracts.DTOs;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IAuthService authService,
    IWebHostEnvironment environment,
    Db.EventPlatformDbContext context,
    IImageService imageService
) : ControllerBase
{
    private const string SessionCookieName = "session";
    private const int SessionMaxAgeDays = 90;
    private const int MagicLinkLimit = 2;
    private static readonly TimeSpan MagicLinkWindow = TimeSpan.FromMinutes(2);

    [HttpPost("magic-link")]
    public async Task<IActionResult> RequestMagicLink(
        [FromBody] MagicLinkRequest request,
        [FromServices] StackExchange.Redis.IConnectionMultiplexer redis)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ApiError(400, "Email is required", HttpContext.TraceIdentifier));

        var emailKey = $"ratelimit:magic-link:{request.Email.Trim().ToLowerInvariant()}";
        var db = redis.GetDatabase();
        var count = await db.StringIncrementAsync(emailKey);
        if (count == 1)
            await db.KeyExpireAsync(emailKey, MagicLinkWindow);

        if (count > MagicLinkLimit)
        {
            var ttl = await db.KeyTimeToLiveAsync(emailKey);
            var retryAfter = (int)Math.Ceiling((ttl ?? MagicLinkWindow).TotalSeconds);
            return StatusCode(429, new { statusCode = 429, message = "Too many requests. Please try again shortly.", retryAfterSeconds = retryAfter });
        }

        var response = await authService.SendMagicLinkAsync(request.Email, request.ReturnUrl, request.FrontendOrigin);
        return Ok(response);
    }

    [HttpPost("magic-link/verify")]
    public async Task<IActionResult> VerifyMagicLink([FromBody] MagicLinkVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new ApiError(400, "Token is required", HttpContext.TraceIdentifier));

        try
        {
            var deviceName = ParseDeviceName(Request.Headers.UserAgent.ToString());
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (user, sessionToken) = await authService.VerifyMagicLinkAsync(request.Token, deviceName, ip);
            SetSessionCookie(sessionToken);

            return Ok(new AuthResponse(User: user));
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "[Auth] VerifyMagicLink failed: {Message}", ex.Message);
            return Unauthorized(new ApiError(401, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("dev-login")]
    public async Task<IActionResult> DevLogin([FromBody] DevLoginRequest request)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ApiError(400, "Email is required", HttpContext.TraceIdentifier));

        try
        {
            var deviceName = ParseDeviceName(Request.Headers.UserAgent.ToString());
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (user, sessionToken) = await authService.DevLoginAsync(request.Email, deviceName, ip);
            SetSessionCookie(sessionToken);

            return Ok(new AuthResponse(User: user));
        }
        catch (KeyNotFoundException ex)
        {
            Log.Warning(ex, "[Auth] DevLogin failed: {Message}", ex.Message);
            return NotFound(new ApiError(404, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var sessionToken = Request.Cookies[SessionCookieName];
        if (!string.IsNullOrEmpty(sessionToken))
        {
            var sessionHash = HashToken(sessionToken);
            await authService.LogoutAsync(sessionHash);
        }

        Response.Cookies.Delete(SessionCookieName);
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var sessionToken = Request.Cookies[SessionCookieName];
        var currentHash = sessionToken is not null ? HashToken(sessionToken) : null;

        var sessions = await authService.GetSessionsAsync(userId.Value, currentHash);
        return Ok(sessions);
    }

    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await authService.RevokeSessionAsync(id, userId.Value);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError(404, "Session not found", HttpContext.TraceIdentifier));
        }
    }

    [HttpDelete("sessions")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var sessionToken = Request.Cookies[SessionCookieName];
        var currentHash = sessionToken is not null ? HashToken(sessionToken) : null;

        await authService.RevokeAllSessionsAsync(userId.Value, currentHash);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new ApiError(401, "Invalid token", HttpContext.TraceIdentifier));

        var user = await authService.GetCurrentUserAsync(userId.Value);
        if (user is null)
            return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        return Ok(user);
    }

    [HttpPut("profile")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new ApiError(401, "Invalid token", HttpContext.TraceIdentifier));

        var user = await context.Users.Include(u => u.Address).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;
        if (!string.IsNullOrWhiteSpace(request.LastName))
            user.LastName = request.LastName;

        if (request.Address is not null || request.City is not null || request.State is not null || request.ZipCode is not null)
        {
            if (user.Address is null)
            {
                var address = new Db.Entities.Address
                {
                    Id = Guid.NewGuid(),
                    Line1 = request.Address ?? "",
                    City = request.City ?? "",
                    State = request.State ?? "",
                    ZipCode = request.ZipCode ?? ""
                };
                context.Set<Db.Entities.Address>().Add(address);
                user.AddressId = address.Id;
                user.Address = address;
            }
            else
            {
                if (request.Address is not null) user.Address.Line1 = request.Address;
                if (request.City is not null) user.Address.City = request.City;
                if (request.State is not null) user.Address.State = request.State;
                if (request.ZipCode is not null) user.Address.ZipCode = request.ZipCode;
            }
        }

        user.Phone = request.Phone;

        if (request.OptInLocationEmail.HasValue)
            user.OptInLocationEmail = request.OptInLocationEmail.Value;

        user.HasCompletedOnboarding = true;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return Ok(new { message = "Profile updated successfully" });
    }

    [HttpPost("me/avatar")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new ApiError(401, "Invalid token", HttpContext.TraceIdentifier));

        var user = await context.Users.FindAsync(userId);
        if (user is null)
            return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        var oldImages = await imageService.GetByEntityAsync("user", userId.Value);
        foreach (var old in oldImages)
            await imageService.DeleteAsync(old.Id);

        var result = await imageService.UploadAsync(file.OpenReadStream(), file.FileName, "user", userId.Value, userId.Value);

        user.AvatarPath = result.StorageKey;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(result);
    }

    [HttpDelete("me/avatar")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> DeleteAvatar()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new ApiError(401, "Invalid token", HttpContext.TraceIdentifier));

        var user = await context.Users.FindAsync(userId);
        if (user is null)
            return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        var images = await imageService.GetByEntityAsync("user", userId.Value);
        foreach (var img in images)
            await imageService.DeleteAsync(img.Id);

        user.AvatarPath = null;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private void SetSessionCookie(string sessionToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(SessionMaxAgeDays)
        };
        Response.Cookies.Append(SessionCookieName, sessionToken, cookieOptions);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private static string HashToken(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string ParseDeviceName(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";

        var browser = "Browser";
        if (userAgent.Contains("Chrome") && !userAgent.Contains("Edg")) browser = "Chrome";
        else if (userAgent.Contains("Edg")) browser = "Edge";
        else if (userAgent.Contains("Firefox")) browser = "Firefox";
        else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome")) browser = "Safari";

        var os = "Unknown";
        if (userAgent.Contains("Windows")) os = "Windows";
        else if (userAgent.Contains("Mac")) os = "macOS";
        else if (userAgent.Contains("Linux")) os = "Linux";
        else if (userAgent.Contains("Android")) os = "Android";
        else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) os = "iOS";

        return $"{browser} on {os}";
    }
}

public record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Phone,
    bool? OptInLocationEmail
);
