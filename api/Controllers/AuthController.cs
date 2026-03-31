using System.Security.Claims;
using Api.Middleware;
using Api.Services;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IAuthService authService,
    IWebHostEnvironment environment,
    Db.EventPlatformDbContext context
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

    [HttpPut("profile")]
    [Authorize]
    [RequireRole(UserRole.User)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        var user = await context.Users.Include(u => u.Address).FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound(new { message = "User not found" });

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;
        if (!string.IsNullOrWhiteSpace(request.LastName))
            user.LastName = request.LastName;

        // Update address fields
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
