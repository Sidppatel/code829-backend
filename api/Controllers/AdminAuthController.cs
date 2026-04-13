using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("admin/auth")]
public class AdminAuthController(
    IAdminAuthService adminAuthService,
    IInvitationService invitationService
) : ControllerBase
{
    private const string SessionCookieName = "session";
    private const int SessionMaxAgeDays = 90;

    [HttpGet("invitation/{token}")]
    public async Task<IActionResult> GetInvitationInfo(string token)
    {
        var info = await invitationService.GetInfoAsync(Uri.UnescapeDataString(token));
        if (info is null)
            return NotFound(new ApiError(404, "Invalid or expired invitation", HttpContext.TraceIdentifier));
        return Ok(info);
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] AcceptInvitationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(new ApiError(400, "All fields are required", HttpContext.TraceIdentifier));

        if (request.Password.Length < 8)
            return BadRequest(new ApiError(400, "Password must be at least 8 characters", HttpContext.TraceIdentifier));

        try
        {
            var deviceName = ParseDeviceName(Request.Headers.UserAgent.ToString());
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (user, sessionToken, jwt) = await invitationService.AcceptAsync(
                request.Token, request.Password, request.FirstName, request.LastName,
                deviceName, ip);

            SetSessionCookie(sessionToken);
            return Ok(new AdminAuthResponse(user, jwt));
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new ApiError(400, ex.Message, HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError(409, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new ApiError(400, "Email and password are required", HttpContext.TraceIdentifier));

        try
        {
            var deviceName = ParseDeviceName(Request.Headers.UserAgent.ToString());
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (user, sessionToken, jwt) = await adminAuthService.LoginAsync(
                request.Email, request.Password, deviceName, ip);

            SetSessionCookie(sessionToken);

            return Ok(new AdminAuthResponse(user, jwt));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ApiError(401, "Invalid email or password", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> Logout()
    {
        var sessionToken = Request.Cookies[SessionCookieName];
        if (!string.IsNullOrEmpty(sessionToken))
        {
            var sessionHash = HashToken(sessionToken);
            await adminAuthService.LogoutAsync(sessionHash);
        }

        Response.Cookies.Delete(SessionCookieName);
        return Ok(new { message = "Logged out" });
    }

    [HttpGet("me")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var admin = await adminAuthService.GetCurrentAdminAsync(userId);
        if (admin is null) return NotFound(new ApiError(404, "Admin user not found", HttpContext.TraceIdentifier));
        return Ok(admin);
    }

    [HttpPut("profile")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateAdminUserRequest request,
        [FromServices] Db.Repositories.StoredProcedures.IAdminUserProcedures adminProc)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await adminProc.UpdateAsync(userId, firstName: request.FirstName, lastName: request.LastName, phone: request.Phone);

        var admin = await adminAuthService.GetCurrentAdminAsync(userId);
        return Ok(admin);
    }

    [HttpPut("password")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeAdminPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new ApiError(400, "Both current and new passwords are required", HttpContext.TraceIdentifier));

        if (request.NewPassword.Length < 8)
            return BadRequest(new ApiError(400, "Password must be at least 8 characters", HttpContext.TraceIdentifier));

        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await adminAuthService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            return Ok(new { message = "Password changed" });
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(new ApiError(400, "Current password is incorrect", HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("sessions")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> GetSessions()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var sessionToken = Request.Cookies[SessionCookieName];
        var currentHash = sessionToken is not null ? HashToken(sessionToken) : null;
        var sessions = await adminAuthService.GetSessionsAsync(userId, currentHash);
        return Ok(sessions);
    }

    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await adminAuthService.RevokeSessionAsync(id, userId);
            return Ok(new { message = "Session revoked" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ApiError(404, "Session not found", HttpContext.TraceIdentifier));
        }
    }

    [HttpDelete("sessions")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var sessionToken = Request.Cookies[SessionCookieName];
        var currentHash = sessionToken is not null ? HashToken(sessionToken) : null;
        await adminAuthService.RevokeAllSessionsAsync(userId, currentHash);
        return Ok(new { message = "All other sessions revoked" });
    }

    private void SetSessionCookie(string sessionToken)
    {
        Response.Cookies.Append(SessionCookieName, sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(SessionMaxAgeDays),
            Path = "/"
        });
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string ParseDeviceName(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Unknown";

        var browser = userAgent switch
        {
            _ when userAgent.Contains("Edg/") => "Edge",
            _ when userAgent.Contains("Chrome/") => "Chrome",
            _ when userAgent.Contains("Firefox/") => "Firefox",
            _ when userAgent.Contains("Safari/") => "Safari",
            _ => "Browser"
        };

        var os = userAgent switch
        {
            _ when userAgent.Contains("Windows") => "Windows",
            _ when userAgent.Contains("Mac OS") => "macOS",
            _ when userAgent.Contains("Linux") => "Linux",
            _ when userAgent.Contains("Android") => "Android",
            _ when userAgent.Contains("iPhone") || userAgent.Contains("iPad") => "iOS",
            _ => "Unknown"
        };

        return $"{browser} on {os}";
    }
}
