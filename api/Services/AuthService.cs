using System.Security.Cryptography;
using System.Text;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

namespace Api.Services;

public class AuthService(
    EventPlatformDbContext context,
    IUserRepository userRepository,
    IAuthProcedures authProc,
    ISettingsService settingsService,
    IEmailService emailService,
    IEncryptionService encryptionService,
    IWebHostEnvironment environment,
    IFileStorageService fileStorage,
    IConnectionMultiplexer redis
) : IAuthService
{
    public async Task<MagicLinkResponse> SendMagicLinkAsync(string email, string? returnUrl = null, string? frontendOrigin = null)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiryMinutes = int.Parse(
            await settingsService.GetOrDefaultAsync("magic_link_expiry_minutes", "15") ?? "15");

        await authProc.CreateMagicLinkAsync(normalizedEmail, tokenHash, DateTime.UtcNow.AddMinutes(expiryMinutes));

        var frontendUrl = frontendOrigin ?? await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var verifyUrl = $"{frontendUrl}/auth/verify?token={Uri.EscapeDataString(rawToken)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
            verifyUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";

        await emailService.SendAsync(
            normalizedEmail,
            $"Your {appName} login link",
            EmailTemplates.MagicLink(appName, verifyUrl, expiryMinutes)
        );

        Log.Information("[Auth] Magic link sent to {Email}", normalizedEmail);

        if (environment.IsDevelopment())
            return new MagicLinkResponse("Magic link sent. Check your email.", rawToken);

        return new MagicLinkResponse("Magic link sent. Check your email.");
    }

    public async Task<(UserDto User, string SessionToken)> VerifyMagicLinkAsync(string token, string? deviceName, string? ip)
    {
        var tokenHash = HashToken(token);

        var result = await authProc.ConsumeMagicLinkAsync(tokenHash);
        if (result is null)
            throw new UnauthorizedAccessException("Invalid or expired magic link token");

        var userId = await authProc.UpsertUserAsync(
            result.Email,
            encryptionService.HashEmail(result.Email),
            result.Email.Split('@')[0],
            "",
            UserRole.User.ToString());

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User creation failed");

        var (sessionToken, _) = await CreateDeviceSessionAsync(userId, deviceName, ip);
        var userDto = MapUserDto(user);

        Log.Information("[Auth] Magic link verified for {Email}", result.Email);
        return (userDto, sessionToken);
    }

    public async Task<(UserDto User, string SessionToken)> DevLoginAsync(string email, string? deviceName, string? ip)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Dev login is not available in this environment");

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await userRepository.GetByEmailAsync(normalizedEmail)
            ?? throw new KeyNotFoundException($"Dev user '{normalizedEmail}' not found. Run seed first.");

        await authProc.UpdateUserLastLoginAsync(user.Id);

        var (sessionToken, _) = await CreateDeviceSessionAsync(user.Id, deviceName, ip);
        var userDto = MapUserDto(user);

        Log.Information("[Auth] Dev login for {Email} ({Role})", user.Email, user.Role);
        return (userDto, sessionToken);
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        return user is null ? null : MapUserDto(user);
    }

    public async Task LogoutAsync(string sessionHash)
    {
        await authProc.RevokeDeviceSessionAsync(sessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{sessionHash}");
    }

    public async Task<List<DeviceSessionDto>> GetSessionsAsync(Guid userId, string? currentSessionHash)
    {
        var sessions = await context.DeviceSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();

        return sessions.Select(s => new DeviceSessionDto(
            Id: s.Id,
            DeviceName: s.DeviceName,
            IpAddress: s.IpAddress,
            LastActivityAt: s.LastActivityAt,
            CreatedAt: s.CreatedAt,
            IsCurrent: s.SessionHash == currentSessionHash
        )).ToList();
    }

    public async Task RevokeSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await context.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null);

        if (session is null)
            throw new KeyNotFoundException("Session not found");

        await authProc.RevokeDeviceSessionAsync(session.SessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{session.SessionHash}");
    }

    public async Task RevokeAllSessionsAsync(Guid userId, string? exceptSessionHash)
    {
        // Get all active session hashes for Redis cleanup
        var hashes = await context.DeviceSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && (exceptSessionHash == null || s.SessionHash != exceptSessionHash))
            .Select(s => s.SessionHash)
            .ToListAsync();

        await authProc.RevokeAllUserSessionsAsync(userId, exceptSessionHash);

        var db = redis.GetDatabase();
        var keys = hashes.Select(h => (RedisKey)$"session:{h}").ToArray();
        if (keys.Length > 0)
            await db.KeyDeleteAsync(keys);
    }

    private async Task<(string RawToken, string Hash)> CreateDeviceSessionAsync(Guid userId, string? deviceName, string? ip)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var sessionHash = HashToken(rawToken);

        await authProc.CreateDeviceSessionAsync(
            userId, sessionHash, null, deviceName, ip,
            DateTime.UtcNow.AddDays(90));

        return (rawToken, sessionHash);
    }

    private UserDto MapUserDto(User user) => new(
        Id: user.Id,
        Email: user.Email,
        FirstName: user.FirstName,
        LastName: user.LastName,
        Role: user.Role.ToString(),
        CreatedAt: user.CreatedAt,
        Address: user.Address?.Line1,
        City: user.Address?.City,
        State: user.Address?.State,
        ZipCode: user.Address?.ZipCode,
        Phone: user.Phone,
        OptInLocationEmail: user.OptInLocationEmail,
        HasCompletedOnboarding: user.HasCompletedOnboarding,
        AvatarUrl: user.AvatarPath is not null
            ? (user.AvatarPath.StartsWith("http") ? user.AvatarPath : fileStorage.GetPublicUrl(user.AvatarPath))
            : null
    );

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
