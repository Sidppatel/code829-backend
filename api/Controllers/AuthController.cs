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
    Db.Repositories.StoredProcedures.IUserProcedures userProc
) : ControllerBase
{
    // Public user sessions live under their own cookie name — see Api.Helpers.PortalHelper.
    // Matches the X-Portal: user header sent by the public frontend app.
    private const string SessionCookieName = Api.Helpers.PortalHelper.UserCookie;
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

            var (user, sessionToken, jwt) = await authService.VerifyMagicLinkAsync(request.Token, deviceName, ip);
            SetSessionCookie(sessionToken);

            return Ok(new AuthResponse(User: user, Token: jwt));
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

            var (user, sessionToken, jwt) = await authService.DevLoginAsync(request.Email, deviceName, ip);
            SetSessionCookie(sessionToken);

            return Ok(new AuthResponse(User: user, Token: jwt));
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

        var exists = await userProc.GetByIdAsync(userId.Value) is not null;
        if (!exists)
            return NotFound(new ApiError(404, "User not found", HttpContext.TraceIdentifier));

        await userProc.UpdateUserProfileAsync(
            userId.Value,
            string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName,
            string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName,
            request.Phone,
            request.Address,
            request.City,
            request.State,
            request.ZipCode,
            request.OptInLocationEmail);

        return Ok(new { message = "Profile updated successfully" });
    }

    [HttpPost("me/avatar")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public IActionResult UploadAvatar(IFormFile file)
    {
        // TODO Phase 2: rewrite with AvatarImageId FK flow
        return StatusCode(501, new ApiError(501, "Avatar upload not yet implemented", HttpContext.TraceIdentifier));
    }

    [HttpDelete("me/avatar")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public IActionResult DeleteAvatar()
    {
        // TODO Phase 2: rewrite with AvatarImageId FK flow
        return StatusCode(501, new ApiError(501, "Avatar delete not yet implemented", HttpContext.TraceIdentifier));
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
