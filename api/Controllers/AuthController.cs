using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IAuthService authService,
    IWebHostEnvironment environment
) : ControllerBase
{
    /// <summary>
    /// Request a magic link email for passwordless login.
    /// </summary>
    [HttpPost("magic-link")]
    public async Task<IActionResult> RequestMagicLink([FromBody] MagicLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required" });

        var response = await authService.SendMagicLinkAsync(request.Email);
        return Ok(response);
    }

    /// <summary>
    /// Verify a magic link token and return a JWT.
    /// </summary>
    [HttpPost("magic-link/verify")]
    public async Task<IActionResult> VerifyMagicLink([FromBody] MagicLinkVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { message = "Token is required" });

        try
        {
            var response = await authService.VerifyMagicLinkAsync(request.Token);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Development-only instant login. Returns 404 in production.
    /// </summary>
    [HttpPost("dev-login")]
    public async Task<IActionResult> DevLogin([FromBody] DevLoginRequest request)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required" });

        try
        {
            var response = await authService.DevLoginAsync(request.Email);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get the current authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        var user = await authService.GetCurrentUserAsync(userId);
        if (user is null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }
}
