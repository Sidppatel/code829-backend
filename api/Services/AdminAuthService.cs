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
    IAdminUserProcedures adminProc,
    IAuthProcedures authProc,
    IFileStorageService fileStorage,
    IConnectionMultiplexer redis,
    IJwtService jwtService
) : IAdminAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<(AdminUserDto User, string SessionToken, string Jwt)> LoginAsync(string email, string password, string? deviceName, string? ip)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        var admin = await context.AdminUsers
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail);

        if (admin is null || !admin.IsActive)
            throw new UnauthorizedAccessException("Invalid email or password");

        // Check account lockout
        if (admin.LockedUntil.HasValue && admin.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((admin.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedAccessException($"Account is locked. Try again in {remaining} minute(s).");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            admin.FailedLoginAttempts++;
            if (admin.FailedLoginAttempts >= MaxFailedAttempts)
            {
                admin.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                Log.Warning("[AdminAuth] Account locked for {Email} after {Attempts} failed attempts", admin.Email, admin.FailedLoginAttempts);
            }
            await context.SaveChangesAsync();
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Reset lockout on successful login
        if (admin.FailedLoginAttempts > 0)
        {
            admin.FailedLoginAttempts = 0;
            admin.LockedUntil = null;
            await context.SaveChangesAsync();
        }

        await adminProc.UpdateLastLoginAsync(admin.Id);

        var (sessionToken, _) = await CreateDeviceSessionAsync(admin.Id, deviceName, ip);
        var dto = MapAdminUserDto(admin);
        var jwt = await jwtService.GenerateAdminJwtAsync(admin);

        Log.Information("[AdminAuth] Login for {Email} ({Role})", admin.Email, admin.Role);
        return (dto, sessionToken, jwt);
    }

    public async Task<AdminUserDto?> GetCurrentAdminAsync(Guid adminUserId)
    {
        var admin = await context.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == adminUserId);
        return admin is null ? null : MapAdminUserDto(admin);
    }

    public async Task LogoutAsync(string sessionHash)
    {
        await authProc.RevokeDeviceSessionAsync(sessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{sessionHash}");
    }

    public async Task<List<DeviceSessionDto>> GetSessionsAsync(Guid adminUserId, string? currentSessionHash)
    {
        var sessions = await context.DeviceSessions
            .AsNoTracking()
            .Where(s => s.AdminUserId == adminUserId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
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

    public async Task RevokeSessionAsync(Guid sessionId, Guid adminUserId)
    {
        var session = await context.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.AdminUserId == adminUserId && s.RevokedAt == null);

        if (session is null)
            throw new KeyNotFoundException("Session not found");

        await authProc.RevokeDeviceSessionAsync(session.SessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{session.SessionHash}");
    }

    public async Task RevokeAllSessionsAsync(Guid adminUserId, string? exceptSessionHash)
    {
        var hashes = await context.DeviceSessions
            .Where(s => s.AdminUserId == adminUserId && s.RevokedAt == null && (exceptSessionHash == null || s.SessionHash != exceptSessionHash))
            .Select(s => s.SessionHash)
            .ToListAsync();

        await adminProc.RevokeAllSessionsAsync(adminUserId, exceptSessionHash);

        var db = redis.GetDatabase();
        var keys = hashes.Select(h => (RedisKey)$"session:{h}").ToArray();
        if (keys.Length > 0)
            await db.KeyDeleteAsync(keys);
    }

    public async Task ChangePasswordAsync(Guid adminUserId, string currentPassword, string newPassword)
    {
        var admin = await context.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == adminUserId)
            ?? throw new KeyNotFoundException("Admin user not found");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, admin.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await adminProc.UpdatePasswordAsync(adminUserId, newHash);

        Log.Information("[AdminAuth] Password changed for {Email}", admin.Email);
    }

    private async Task<(string RawToken, string Hash)> CreateDeviceSessionAsync(Guid adminUserId, string? deviceName, string? ip)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var sessionHash = HashToken(rawToken);

        await adminProc.CreateDeviceSessionAsync(
            adminUserId, sessionHash, null, deviceName, ip,
            DateTime.UtcNow.AddDays(90));

        return (rawToken, sessionHash);
    }

    private AdminUserDto MapAdminUserDto(AdminUser admin) => new(
        Id: admin.Id,
        Email: admin.Email,
        FirstName: admin.FirstName,
        LastName: admin.LastName,
        Role: admin.Role.ToString(),
        IsActive: admin.IsActive,
        CreatedAt: admin.CreatedAt,
        LastLoginAt: admin.LastLoginAt,
        Phone: admin.Phone,
        AvatarUrl: admin.AvatarPath is not null
            ? (admin.AvatarPath.StartsWith("http") ? admin.AvatarPath : fileStorage.GetPublicUrl(admin.AvatarPath))
            : null
    );

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
