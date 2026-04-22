using System.Security.Cryptography;
using System.Text;
using Contracts.DTOs.Auth;
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
    IUserProcedures userProc,
    ISettingsService settingsService,
    IEmailService emailService,
    IEncryptionService encryptionService,
    IWebHostEnvironment environment,
    IFileStorageService fileStorage,
    IConnectionMultiplexer redis,
    IJwtService jwtService
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

    public async Task<(UserDto User, string SessionToken, string Jwt)> VerifyMagicLinkAsync(string token, string? deviceName, string? ip)
    {
        var tokenHash = HashToken(token);

        var result = await authProc.ConsumeMagicLinkAsync(tokenHash);
        if (result is null)
            throw new UnauthorizedAccessException("Invalid or expired magic link token");

        var userId = await authProc.UpsertUserAsync(
            result.Email,
            encryptionService.HashEmail(result.Email),
            result.Email.Split('@')[0],
            "");

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User creation failed");

        var (sessionToken, _) = await CreateDeviceSessionAsync(userId, deviceName, ip);
        var userDto = MapUserDto(user);
        var jwt = await jwtService.GenerateUserJwtAsync(user);

        Log.Information("[Auth] Magic link verified for {Email}", result.Email);
        return (userDto, sessionToken, jwt);
    }

    public async Task<(UserDto User, string SessionToken, string Jwt)> DevLoginAsync(string email, string? deviceName, string? ip)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Dev login is not available in this environment");

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await userRepository.GetByEmailAsync(normalizedEmail)
            ?? throw new KeyNotFoundException($"Dev user '{normalizedEmail}' not found. Run seed first.");

        await authProc.UpdateUserLastLoginAsync(user.Id);

        var (sessionToken, _) = await CreateDeviceSessionAsync(user.Id, deviceName, ip);
        var userDto = MapUserDto(user);
        var jwt = await jwtService.GenerateUserJwtAsync(user);

        Log.Information("[Auth] Dev login for {Email}", user.Email);
        return (userDto, sessionToken, jwt);
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
        var sessions = await context.DeviceSessionViews
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
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

    public async Task RevokeSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await context.DeviceSessionViews
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DeviceSessionId == sessionId && s.UserId == userId && s.RevokedAt == null);

        if (session is null)
            throw new KeyNotFoundException("Session not found");

        await authProc.RevokeDeviceSessionAsync(session.SessionHash);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{session.SessionHash}");
    }

    public async Task RevokeAllSessionsAsync(Guid userId, string? exceptSessionHash)
    {
        // Get all active session hashes for Redis cleanup
        var hashes = await context.DeviceSessionViews
            .AsNoTracking()
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
        UserId: user.Id,
        Email: user.Email,
        FirstName: user.FirstName,
        LastName: user.LastName,
        Role: "User",
        CreatedAt: user.CreatedAt,
        Address: user.Address?.Line1,
        City: user.Address?.City,
        State: user.Address?.State,
        ZipCode: user.Address?.ZipCode,
        Phone: user.Phone,
        OptInLocationEmail: user.OptInLocationEmail,
        HasCompletedOnboarding: user.HasCompletedOnboarding,
        ImageUrl: user.Image?.StorageKey is not null
            ? fileStorage.GetPublicUrl($"{user.Image.StorageKey}.webp")
            : null
    );

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    // ── Email+password auth ───────────────────────────────

    public async Task<SignupResponse> SignupAsync(string email, string firstName, string lastName, string password, string? ip, string? frontendOrigin)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var emailHash = encryptionService.HashEmail(normalizedEmail);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        Db.Entities.User user;
        try
        {
            user = await userProc.SignupUserAsync(normalizedEmail, emailHash, firstName.Trim(), lastName.Trim(), passwordHash);
        }
        catch (Npgsql.PostgresException ex) when (ex.MessageText.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("[Auth] Signup attempted for existing email: {Email}", normalizedEmail);
            throw new InvalidOperationException("An account with that email already exists");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiryMinutes = int.Parse(
            await settingsService.GetOrDefaultAsync("email_verification_expiry_minutes", "60") ?? "60");

        await userProc.CreateEmailVerificationTokenAsync(user.Id, tokenHash, DateTime.UtcNow.AddMinutes(expiryMinutes), ip);

        var frontendUrl = frontendOrigin ?? await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var verifyUrl = $"{frontendUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";

        await emailService.SendAsync(
            normalizedEmail,
            $"Confirm your {appName} email",
            EmailTemplates.EmailVerification(appName, user.FirstName, verifyUrl, expiryMinutes));

        Log.Information("[Auth] Signup + verification email sent for {Email}", normalizedEmail);

        if (environment.IsDevelopment())
            return new SignupResponse("Account created. Check your email to verify.", rawToken);

        return new SignupResponse("Account created. Check your email to verify.");
    }

    public async Task<(UserDto User, string SessionToken, string Jwt)> SigninAsync(string email, string password, string? deviceName, string? ip)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var emailHash = encryptionService.HashEmail(normalizedEmail);

        var user = await userProc.GetByEmailForSigninAsync(emailHash);

        if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        if (!user.EmailVerified)
            throw new UnauthorizedAccessException("Please verify your email before signing in. Check your inbox for the verification link.");

        await userProc.UpdateLastLoginAsync(user.Id);

        var fullUser = await userRepository.GetByIdAsync(user.Id)
            ?? throw new InvalidOperationException("User lookup failed after signin");

        var (sessionToken, _) = await CreateDeviceSessionAsync(fullUser.Id, deviceName, ip);
        var userDto = MapUserDto(fullUser);
        var jwt = await jwtService.GenerateUserJwtAsync(fullUser);

        Log.Information("[Auth] Signin for {Email}", normalizedEmail);
        return (userDto, sessionToken, jwt);
    }

    public async Task<(UserDto User, string SessionToken, string Jwt)> VerifyEmailAsync(string token, string? deviceName, string? ip)
    {
        var tokenHash = HashToken(token);

        Db.Entities.User user;
        try
        {
            user = await userProc.ConsumeEmailVerificationTokenAsync(tokenHash);
        }
        catch (Npgsql.PostgresException ex) when (ex.MessageText.Contains("Invalid or expired", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid or expired verification token");
        }

        var fullUser = await userRepository.GetByIdAsync(user.Id)
            ?? throw new InvalidOperationException("User lookup failed after email verification");

        var (sessionToken, _) = await CreateDeviceSessionAsync(fullUser.Id, deviceName, ip);
        var userDto = MapUserDto(fullUser);
        var jwt = await jwtService.GenerateUserJwtAsync(fullUser);

        Log.Information("[Auth] Email verified + auto-signin for {Email}", fullUser.Email);
        return (userDto, sessionToken, jwt);
    }

    public async Task RequestPasswordResetAsync(string email, string? ip, string? frontendOrigin)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var emailHash = encryptionService.HashEmail(normalizedEmail);
        var user = await userProc.GetByEmailForSigninAsync(emailHash);

        // Silently succeed for unknown/inactive email to avoid leaking account existence.
        if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
        {
            Log.Warning("[Auth] Password reset requested for unknown/inactive/magic-link-only email: {Email}", normalizedEmail);
            return;
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiryMinutes = int.Parse(
            await settingsService.GetOrDefaultAsync("password_reset_expiry_minutes", "60") ?? "60");

        await userProc.CreatePasswordResetTokenAsync(user.Id, tokenHash, DateTime.UtcNow.AddMinutes(expiryMinutes), ip);

        var frontendUrl = frontendOrigin ?? await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var resetUrl = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        await emailService.SendAsync(
            normalizedEmail,
            $"{appName} password reset",
            EmailTemplates.PasswordReset(appName, resetUrl, expiryMinutes));

        Log.Information("[Auth] Password reset link sent to {Email}", normalizedEmail);
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        var tokenHash = HashToken(token);

        Db.Entities.User user;
        try
        {
            user = await userProc.ConsumePasswordResetTokenAsync(tokenHash);
        }
        catch (Npgsql.PostgresException ex) when (ex.MessageText.Contains("Invalid or expired", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid or expired reset token");
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await userProc.UpdatePasswordAsync(user.Id, newHash);

        await RevokeAllSessionsAsync(user.Id, null);

        Log.Information("[Auth] Password reset successful for {Email}", user.Email);
    }
}
