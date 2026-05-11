using System.Security.Cryptography;
using System.Text;
using Contracts.DTOs.Auth;
using Db;
using Db.Entities;
using Db.Repositories;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Sentry;
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
    IJwtService jwtService,
    ISecretsProvider secretsProvider,
    IImageService imageService,
    IHttpClientFactory httpClientFactory
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

        Log.Information("[Auth] Magic link sent to {EmailHash}", HashEmailForLog(normalizedEmail));

        if (environment.IsDevelopment())
            Log.Debug("[Auth] Magic link verify URL (dev only): {Url}", verifyUrl);

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

        var rldb = redis.GetDatabase();
        await rldb.KeyDeleteAsync($"ratelimit:mlv:{tokenHash}");

        Log.Information("[Auth] Magic link verified for {EmailHash}", HashEmailForLog(result.Email));
        return (userDto, sessionToken, jwt);
    }

#if DEBUG
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

        Log.Information("[Auth] Dev login for {EmailHash}", HashEmailForLog(user.Email));
        return (userDto, sessionToken, jwt);
    }
#endif

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
            : null,
        HasPassword: !string.IsNullOrEmpty(user.PasswordHash),
        HasGoogle: !string.IsNullOrEmpty(user.GoogleSubject),
        EmailVerified: user.EmailVerified
    );

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string HashEmailForLog(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email));
        return Convert.ToHexStringLower(bytes)[..12];
    }

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
            Log.Warning("[Auth] Signup attempted for existing email: {EmailHash}", HashEmailForLog(normalizedEmail));
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

        Log.Information("[Auth] Signup + verification email sent for {EmailHash}", HashEmailForLog(normalizedEmail));

        if (environment.IsDevelopment())
            Log.Debug("[Auth] Email verification URL (dev only): {Url}", verifyUrl);

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

        Log.Information("[Auth] Signin for {EmailHash}", HashEmailForLog(normalizedEmail));
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

        Log.Information("[Auth] Email verified + auto-signin for {EmailHash}", HashEmailForLog(fullUser.Email));
        return (userDto, sessionToken, jwt);
    }

    public async Task RequestPasswordResetAsync(string email, string? ip, string? frontendOrigin)
    {

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var emailHashLog = HashEmailForLog(normalizedEmail);

        try
        {
            var emailHash = encryptionService.HashEmail(normalizedEmail);

            Log.Debug("[Auth] forgot-password step=lookup emailHash={EmailHash}", emailHashLog);
            var user = await userProc.GetByEmailForSigninAsync(emailHash);

            if (user is null || !user.IsActive)
            {
                Log.Warning("[Auth] Password reset requested for unknown/inactive emailHash={EmailHash}", emailHashLog);
                return;
            }

            Log.Debug("[Auth] forgot-password step=create_token userId={UserId}", user.Id);
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = Convert.ToBase64String(tokenBytes);
            var tokenHash = HashToken(rawToken);

            var expiryMinutes = int.Parse(
                await settingsService.GetOrDefaultAsync("password_reset_expiry_minutes", "60") ?? "60");

            await userProc.CreatePasswordResetTokenAsync(user.Id, tokenHash, DateTime.UtcNow.AddMinutes(expiryMinutes), ip);

            Log.Debug("[Auth] forgot-password step=send_email userId={UserId}", user.Id);
            var frontendUrl = frontendOrigin ?? await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
            var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
            var resetUrl = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

            try
            {
                await emailService.SendAsync(
                    normalizedEmail,
                    $"{appName} password reset",
                    EmailTemplates.PasswordReset(appName, resetUrl, expiryMinutes));

                Log.Information("[Auth] Password reset link sent to {EmailHash}", emailHashLog);
            }
            catch (Exception emailEx)
            {
                SentrySdk.CaptureException(emailEx);
                Log.Error(emailEx, "[Auth] forgot-password email send failed emailHash={EmailHash}", emailHashLog);
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Log.Error(ex, "[Auth] forgot-password internal failure emailHash={EmailHash}", emailHashLog);
        }
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

        Log.Information("[Auth] Password reset successful for {EmailHash}", HashEmailForLog(user.Email));
    }

    public async Task<(UserDto User, string SessionToken, string Jwt)> SignInWithGoogleAsync(string? credential, string? code, string? deviceName, string? ip)
    {
        var clientId = secretsProvider.GoogleOAuthClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google sign-in is not configured");

        var idToken = credential;
        if (string.IsNullOrWhiteSpace(idToken) && !string.IsNullOrWhiteSpace(code))
            idToken = await ExchangeGoogleCodeForIdTokenAsync(code, clientId);

        if (string.IsNullOrWhiteSpace(idToken))
            throw new UnauthorizedAccessException("Missing Google credential or code");

        Google.Apis.Auth.GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken,
                new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
        }
        catch (Google.Apis.Auth.InvalidJwtException ex)
        {
            Log.Warning(ex, "[Auth] Google credential rejected");
            throw new UnauthorizedAccessException("Invalid Google credential");
        }

        if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            throw new UnauthorizedAccessException("Google credential missing subject or email");

        var normalizedEmail = payload.Email.ToLowerInvariant().Trim();
        var emailHash = encryptionService.HashEmail(normalizedEmail);
        var firstName = string.IsNullOrWhiteSpace(payload.GivenName) ? normalizedEmail.Split('@')[0] : payload.GivenName.Trim();
        var lastName = payload.FamilyName?.Trim() ?? "";

        Db.Entities.User user;
        try
        {
            user = await userProc.SignInUserGoogleAsync(payload.Subject, normalizedEmail, emailHash, firstName, lastName);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0002")
        {
            Log.Warning("[Auth] Google sign-in blocked: existing password account requires password login first {EmailHash}", HashEmailForLog(normalizedEmail));
            throw new InvalidOperationException("PASSWORD_ACCOUNT_LINK_REQUIRED");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0001")
        {
            Log.Warning("[Auth] Google sign-in conflict: subject already linked elsewhere {EmailHash}", HashEmailForLog(normalizedEmail));
            throw new UnauthorizedAccessException("Google account already linked to a different identity");
        }

        var fullUser = await userRepository.GetByIdAsync(user.Id)
            ?? throw new InvalidOperationException("User lookup failed after Google sign-in");

        if (fullUser.ImageId is null && !string.IsNullOrWhiteSpace(payload.Picture))
        {
            try
            {
                await imageService.IngestFromUrlAsync(payload.Picture, fullUser.Id);
                fullUser = await userRepository.GetByIdAsync(user.Id) ?? fullUser;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Auth] Google profile picture ingest failed for {EmailHash}", HashEmailForLog(normalizedEmail));
            }
        }

        var (sessionToken, _) = await CreateDeviceSessionAsync(fullUser.Id, deviceName, ip);
        var userDto = MapUserDto(fullUser);
        var jwt = await jwtService.GenerateUserJwtAsync(fullUser);

        Log.Information("[Auth] Google sign-in for {EmailHash}", HashEmailForLog(normalizedEmail));
        return (userDto, sessionToken, jwt);
    }

    private async Task<string> ExchangeGoogleCodeForIdTokenAsync(string code, string clientId)
    {
        var clientSecret = secretsProvider.GoogleOAuthClientSecret;
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Google OAuth client secret not configured");

        var http = httpClientFactory.CreateClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("redirect_uri", "postmessage"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
        });

        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", form);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("[Auth] Google code exchange failed status={Status} body={Body}", (int)response.StatusCode, body);
            throw new UnauthorizedAccessException("Google code exchange failed");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id_token", out var idTokenEl))
            throw new UnauthorizedAccessException("Google response missing id_token");

        var idToken = idTokenEl.GetString();
        if (string.IsNullOrWhiteSpace(idToken))
            throw new UnauthorizedAccessException("Google response missing id_token");
        return idToken;
    }

    public async Task SetOrChangePasswordAsync(Guid userId, string? currentPassword, string newPassword, string? currentSessionHash)
    {
        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        var hasExistingPassword = !string.IsNullOrEmpty(user.PasswordHash);

        if (hasExistingPassword)
        {
            if (string.IsNullOrEmpty(currentPassword))
                throw new InvalidOperationException("CURRENT_PASSWORD_REQUIRED");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("CURRENT_PASSWORD_INCORRECT");
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await userProc.SetPasswordAsync(userId, newHash, true, currentSessionHash);

        var db = redis.GetDatabase();
        Log.Information("[Auth] Password set/changed for {EmailHash}", HashEmailForLog(user.Email));
    }
}
