using System.Security.Cryptography;
using System.Text;
using Contracts.DTOs.Auth;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

namespace Api.Services;

public class AdminAuthService(
    EventPlatformDbContext context,
    IBusinessUserProcedures businessProc,
    IAuthProcedures authProc,
    IBusinessPasswordResetTokenProcedures pwdResetProc,
    IFileStorageService fileStorage,
    IConnectionMultiplexer redis,
    IJwtService jwtService,
    IEmailService emailService,
    ISettingsService settingsService
) : IAdminAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<(BusinessUserDto User, string SessionToken, string Jwt)> LoginAsync(string email, string password, string? deviceName, string? ip)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        var admin = await businessProc.GetByEmailAsync(normalizedEmail);

        if (admin is null || !admin.IsActive)
            throw new UnauthorizedAccessException("Invalid email or password");

        if (admin.LockedUntil.HasValue && admin.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((admin.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedAccessException($"Account is locked. Try again in {remaining} minute(s).");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            await businessProc.IncrementFailedLoginAsync(admin.Id, MaxFailedAttempts, (int)LockoutDuration.TotalMinutes);
            if (admin.FailedLoginAttempts + 1 >= MaxFailedAttempts)
                Log.Warning("[AdminAuth] Account locked for {Email} after {Attempts} failed attempts", admin.Email, admin.FailedLoginAttempts + 1);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (admin.FailedLoginAttempts > 0)
            await businessProc.ResetLockoutAsync(admin.Id);

        await businessProc.UpdateLastLoginAsync(admin.Id);

        var (sessionToken, _) = await CreateDeviceSessionAsync(admin.Id, deviceName, ip);
        var dto = MapBusinessUserDto(admin);
        var jwt = await jwtService.GenerateAdminJwtAsync(admin);

        Log.Information("[AdminAuth] Login for {Email} ({Role})", admin.Email, admin.Role);
        return (dto, sessionToken, jwt);
    }

    public async Task<BusinessUserDto?> GetCurrentAdminAsync(Guid businessUserId)
    {
        var admin = await businessProc.GetByIdAsync(businessUserId);
        return admin is null ? null : MapBusinessUserDto(admin);
    }

    public async Task LogoutAsync(string sessionHash)
    {
        await authProc.RevokeDeviceSessionAsync(sessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{sessionHash}");
    }

    public async Task<List<DeviceSessionDto>> GetSessionsAsync(Guid businessUserId, string? currentSessionHash)
    {
        var sessions = await context.DeviceSessionViews
            .AsNoTracking()
            .Where(s => s.BusinessUserId == businessUserId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .Take(50)
            .ToListAsync();

        return sessions.Select(s => new DeviceSessionDto(
            DeviceSessionId: s.DeviceSessionId,
            DeviceName: s.DeviceName,
            IpAddress: s.IpAddress,
            LastActivityAt: s.LastActivityAt,
            CreatedAt: s.CreatedAt,
            IsCurrent: s.SessionHash == currentSessionHash
        )).ToList();
    }

    public async Task RevokeSessionAsync(Guid sessionId, Guid businessUserId)
    {
        var session = await context.DeviceSessionViews
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DeviceSessionId == sessionId && s.BusinessUserId == businessUserId && s.RevokedAt == null);

        if (session is null)
            throw new KeyNotFoundException("Session not found");

        await authProc.RevokeDeviceSessionAsync(session.SessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{session.SessionHash}");
    }

    public async Task RevokeAllSessionsAsync(Guid businessUserId, string? exceptSessionHash)
    {
        var hashes = await context.DeviceSessionViews
            .AsNoTracking()
            .Where(s => s.BusinessUserId == businessUserId && s.RevokedAt == null && (exceptSessionHash == null || s.SessionHash != exceptSessionHash))
            .Select(s => s.SessionHash)
            .ToListAsync();

        await businessProc.RevokeAllSessionsAsync(businessUserId, exceptSessionHash);

        var db = redis.GetDatabase();
        var keys = hashes.Select(h => (RedisKey)$"session:{h}").ToArray();
        if (keys.Length > 0)
            await db.KeyDeleteAsync(keys);
    }

    public async Task ChangePasswordAsync(Guid businessUserId, string currentPassword, string newPassword)
    {
        var admin = await businessProc.GetByIdAsync(businessUserId)
            ?? throw new KeyNotFoundException("Business user not found");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, admin.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await businessProc.UpdatePasswordAsync(businessUserId, newHash);

        Log.Information("[AdminAuth] Password changed for {Email}", admin.Email);
    }

    public async Task RequestPasswordResetAsync(string email, string? origin)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var admin = await businessProc.GetByEmailAsync(normalizedEmail);

        if (admin is null || !admin.IsActive)
        {
            Log.Warning("[AdminAuth] Password reset requested for non-existent or inactive business user: {Email}", normalizedEmail);
            throw new UnauthorizedAccessException("Unauthorized");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiryMinutes = int.Parse(
            await settingsService.GetOrDefaultAsync("password_reset_expiry_minutes", "60") ?? "60");

        await pwdResetProc.CreateAsync(
            admin.Id,
            tokenHash,
            DateTime.UtcNow.AddMinutes(expiryMinutes),
            normalizedEmail);

        var frontendUrl = origin ?? await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var resetUrl = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        try
        {
            await emailService.SendAsync(
                normalizedEmail,
                $"{appName} Password Reset",
                EmailTemplates.PasswordReset(appName, resetUrl, expiryMinutes)
            );
            Log.Information("[AdminAuth] Password reset link sent to {Email}", normalizedEmail);
        }
        catch (Exception emailEx)
        {
            Log.Error(emailEx, "[AdminAuth] Password reset email send failed for {Email}", normalizedEmail);
            Sentry.SentrySdk.CaptureException(emailEx);
        }
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        var tokenHash = HashToken(token);
        var resetToken = await pwdResetProc.GetByHashAsync(tokenHash);

        if (resetToken is null || resetToken.IsUsed || resetToken.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired reset token");

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await businessProc.UpdatePasswordAsync(resetToken.BusinessUserId, newHash);

        await pwdResetProc.InvalidateAsync(tokenHash);

        await RevokeAllSessionsAsync(resetToken.BusinessUserId, null);

        Log.Information("[AdminAuth] Password reset successful for {Email}", resetToken.BusinessUserEmail);
    }

    private async Task<(string RawToken, string Hash)> CreateDeviceSessionAsync(Guid businessUserId, string? deviceName, string? ip)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var sessionHash = HashToken(rawToken);

        await businessProc.CreateDeviceSessionAsync(
            businessUserId, sessionHash, null, deviceName, ip,
            DateTime.UtcNow.AddDays(90));

        return (rawToken, sessionHash);
    }

    private BusinessUserDto MapBusinessUserDto(BusinessUser admin) => new(
        BusinessUserId: admin.Id,
        Email: admin.Email,
        FirstName: admin.FirstName,
        LastName: admin.LastName,
        Role: admin.Role.ToString(),
        IsActive: admin.IsActive,
        CreatedAt: admin.CreatedAt,
        LastLoginAt: admin.LastLoginAt,
        Phone: admin.Phone,
        ImageUrl: admin.Image?.StorageKey is not null
            ? fileStorage.GetPublicUrl($"{admin.Image.StorageKey}.webp")
            : null
    );

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
