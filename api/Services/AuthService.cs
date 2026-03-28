using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Api.Services;

public class AuthService(
    EventPlatformDbContext context,
    IUserRepository userRepository,
    ISettingsService settingsService,
    IEmailService emailService,
    IEncryptionService encryptionService,
    IWebHostEnvironment environment
) : IAuthService
{
    public async Task<MagicLinkResponse> SendMagicLinkAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        // Generate cryptographically random token (32 bytes = 256 bits)
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiryMinutes = int.Parse(
            await settingsService.GetOrDefaultAsync("magic_link_expiry_minutes", "15") ?? "15");

        var magicLink = new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Email = normalizedEmail,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            IsUsed = false
        };

        context.MagicLinkTokens.Add(magicLink);
        await context.SaveChangesAsync();

        var frontendUrl = await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var brandName = await settingsService.GetOrDefaultAsync("brand_name", "Code829");
        var verifyUrl = $"{frontendUrl}/auth/verify?token={Uri.EscapeDataString(rawToken)}";

        await emailService.SendAsync(
            normalizedEmail,
            $"Your {brandName} login link",
            $"Click here to log in: {verifyUrl}\n\nThis link expires in {expiryMinutes} minutes."
        );

        Log.Information("[Auth] Magic link sent to {Email}", normalizedEmail);

        // In development, return the raw token for easy testing
        if (environment.IsDevelopment())
            return new MagicLinkResponse("Magic link sent. Check your email.", rawToken);

        return new MagicLinkResponse("Magic link sent. Check your email.");
    }

    public async Task<AuthResponse> VerifyMagicLinkAsync(string token)
    {
        var tokenHash = HashToken(token);

        var magicLink = await context.MagicLinkTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

        if (magicLink is null)
            throw new UnauthorizedAccessException("Invalid or expired magic link token");

        // Mark as used (single-use)
        magicLink.IsUsed = true;
        magicLink.UsedAt = DateTime.UtcNow;

        // Find or create user
        var user = await userRepository.GetByEmailAsync(magicLink.Email);
        if (user is null)
        {
            // Auto-create user on first magic link login
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = magicLink.Email,
                EmailHash = encryptionService.HashEmail(magicLink.Email),
                Name = magicLink.Email.Split('@')[0],
                Role = UserRole.User,
                IsActive = true
            };
            context.Users.Add(user);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> DevLoginAsync(string email)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Dev login is not available in this environment");

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await userRepository.GetByEmailAsync(normalizedEmail)
            ?? throw new KeyNotFoundException($"Dev user '{normalizedEmail}' not found. Run seed first.");

        user.LastLoginAt = DateTime.UtcNow;
        await userRepository.UpdateAsync(user);

        Log.Information("[Auth] Dev login for {Email} ({Role})", user.Email, user.Role);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null) return null;

        return new UserDto(
            Id: user.Id,
            Email: user.Email,
            Name: user.Name,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt
        );
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var jwtSecret = await settingsService.GetAsync("jwt_secret");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(24);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(
            Token: tokenString,
            Email: user.Email,
            Name: user.Name,
            Role: user.Role.ToString(),
            ExpiresAt: expiresAt
        );
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
