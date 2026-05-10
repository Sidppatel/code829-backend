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
using Serilog;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/admin/auth")]
public class AdminAuthController(
    IAdminAuthService adminAuthService,
    IInvitationService invitationService,
    IWebHostEnvironment environment,
    IImageService imageService,
    IFileStorageService fileStorage
) : ControllerBase
{

    private const int SessionMaxAgeDays = 14;

    private string? CurrentPortalCookie() =>
        Helpers.PortalHelper.CookieFor(Helpers.PortalHelper.ReadPortal(Request));

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
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new ApiError(400, "Token and password are required", HttpContext.TraceIdentifier));

        var (pwValid, pwError) = Helpers.PasswordValidator.Validate(request.Password);
        if (!pwValid)
            return BadRequest(new ApiError(400, pwError!, HttpContext.TraceIdentifier));

        try
        {
            var deviceName = ParseDeviceName(Request.Headers.UserAgent.ToString());
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (user, sessionToken, _) = await invitationService.AcceptAsync(
                request.Token, request.Password, request.FirstName, request.LastName,
                deviceName, ip);

            SetSessionCookie(sessionToken);
            return Ok(new AdminAuthResponse(user));
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "[AdminAuth] Signup failed: {Message}", ex.Message);
            return BadRequest(new ApiError(400, "Invalid request", HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "[AdminAuth] Signup conflict: {Message}", ex.Message);
            return Conflict(new ApiError(409, "Operation not allowed", HttpContext.TraceIdentifier));
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

            var (user, sessionToken, _) = await adminAuthService.LoginAsync(
                request.Email, request.Password, deviceName, ip);

            var portal = Helpers.PortalHelper.ReadPortal(Request);
            var minRole = Helpers.PortalHelper.MinRoleForPortal(portal);
            if (minRole.HasValue
                && Enum.TryParse<AdminRole>(user.Role, out var actualRole)
                && (int)actualRole < (int)minRole.Value)
            {
                return StatusCode(403, new ApiError(403,
                    "Your account does not have access to this portal.",
                    HttpContext.TraceIdentifier));
            }

            SetSessionCookie(sessionToken);

            return Ok(new AdminAuthResponse(user));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ApiError(401, "Invalid email or password", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ApiError(400, "Email is required", HttpContext.TraceIdentifier));

        try
        {
            await adminAuthService.RequestPasswordResetAsync(request.Email, Request.Headers.Origin);
        }
        catch (UnauthorizedAccessException)
        {
            Log.Information("[AdminAuth] ForgotPassword: no admin account found for {Email}", request.Email);
        }

        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new ApiError(400, "Token and new password are required", HttpContext.TraceIdentifier));

        var (pwValid, pwError) = Helpers.PasswordValidator.Validate(request.NewPassword);
        if (!pwValid)
            return BadRequest(new ApiError(400, pwError!, HttpContext.TraceIdentifier));

        try
        {
            await adminAuthService.ResetPasswordAsync(request.Token, request.NewPassword);
            return Ok(new { message = "Password reset successful" });
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "[AdminAuth] ResetPassword failed: {Message}", ex.Message);
            return Unauthorized(new ApiError(401, "Invalid or expired credentials", HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> Logout()
    {

        var cookieName = CurrentPortalCookie();
        if (cookieName is null)
            return Ok(new { message = "Logged out" });

        var sessionToken = Request.Cookies[cookieName];
        if (!string.IsNullOrEmpty(sessionToken))
        {
            var sessionHash = HashToken(sessionToken);
            await adminAuthService.LogoutAsync(sessionHash);
        }

        Response.Cookies.Delete(cookieName);
        return Ok(new { message = "Logged out" });
    }

    [HttpGet("me")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var admin = await adminAuthService.GetCurrentAdminAsync(userId);
        if (admin is null) return NotFound(new ApiError(404, "Business user not found", HttpContext.TraceIdentifier));
        return Ok(admin);
    }

    [HttpPut("profile")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateBusinessUserRequest request,
        [FromServices] Db.Repositories.StoredProcedures.IBusinessUserProcedures businessUserProc)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await businessUserProc.UpdateAsync(userId, firstName: request.FirstName, lastName: request.LastName, phone: request.Phone);

        var admin = await adminAuthService.GetCurrentAdminAsync(userId);
        return Ok(admin);
    }

    [HttpPost("me/image")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        var (valid, error) = Helpers.FileUploadValidator.Validate(file);
        if (!valid) return BadRequest(new ApiError(400, error!, HttpContext.TraceIdentifier));

        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var storageKey = await imageService.ReplaceImageAsync(adminId, "business_user", file);
        return Ok(new { url = fileStorage.GetPublicUrl(storageKey) });
    }

    [HttpDelete("me/image")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> DeleteImage()
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await imageService.DeleteImageAsync(adminId, "business_user");
        return NoContent();
    }

    [HttpPut("password")]
    [Authorize]
    [RequireRole(UserRole.Staff)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeBusinessUserPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new ApiError(400, "Both current and new passwords are required", HttpContext.TraceIdentifier));

        var (pwValid, pwError) = Helpers.PasswordValidator.Validate(request.NewPassword);
        if (!pwValid)
            return BadRequest(new ApiError(400, pwError!, HttpContext.TraceIdentifier));

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
        var cookieName = CurrentPortalCookie();
        var sessionToken = cookieName is not null ? Request.Cookies[cookieName] : null;
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
        var cookieName = CurrentPortalCookie();
        var sessionToken = cookieName is not null ? Request.Cookies[cookieName] : null;
        var currentHash = sessionToken is not null ? HashToken(sessionToken) : null;
        await adminAuthService.RevokeAllSessionsAsync(userId, currentHash);
        return Ok(new { message = "All other sessions revoked" });
    }

    private void SetSessionCookie(string sessionToken)
    {

        var cookieName = CurrentPortalCookie() ?? Helpers.PortalHelper.AdminCookie;

        Response.Cookies.Append(cookieName, sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
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
