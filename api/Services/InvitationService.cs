using System.Security.Cryptography;
using System.Text;
using Contracts.DTOs.Auth;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

public class InvitationService(
    EventPlatformDbContext context,
    Db.Repositories.StoredProcedures.IBusinessUserProcedures businessUserProc,
    Db.Repositories.StoredProcedures.IInvitationProcedures invitationProc,
    IEncryptionService encryptionService,
    IEmailService emailService,
    ISettingsService settingsService,
    IFileStorageService fileStorage,
    IJwtService jwtService
) : IInvitationService
{
    private const double InvitationExpiryMinutes = 15;

    public async Task<InvitationDto> CreateAsync(string email, AdminRole role, Guid invitedByBusinessUserId)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        if (await businessUserProc.ExistsByEmailAsync(normalizedEmail))
            throw new InvalidOperationException("A user with this email already exists");

        var existingPending = await invitationProc.GetPendingByEmailAsync(normalizedEmail);
        if (existingPending is not null)
            await invitationProc.RevokeAsync(existingPending.Id);

        var inviter = await businessUserProc.GetByIdAsync(invitedByBusinessUserId)
            ?? throw new KeyNotFoundException("Inviter not found");

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiresAt = DateTime.UtcNow.AddMinutes(InvitationExpiryMinutes);
        var invitationId = await invitationProc.CreateAsync(
            normalizedEmail, tokenHash, role.ToString(), invitedByBusinessUserId, expiresAt);

        var frontendUrl = await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173") ?? "http://localhost:5173";
        var portalBase = BuildPortalUrl(frontendUrl, role);
        var signupUrl = $"{portalBase}/signup?token={Uri.EscapeDataString(rawToken)}";

        var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var inviterName = $"{inviter.FirstName} {inviter.LastName}".Trim();

        Log.Information("[Invitation] Sending invitation to {Email}. Signup URL: {Url}", normalizedEmail, signupUrl);

        try
        {
            await emailService.SendAsync(
                normalizedEmail,
                $"You're invited to join {appName}",
                EmailTemplates.Invitation(appName, inviterName, role.ToString(), signupUrl, (int)InvitationExpiryMinutes)
            );
            Log.Information("[Invitation] {Inviter} invited {Email} as {Role}", inviterName, normalizedEmail, role);
        }
        catch (Exception emailEx)
        {
            Log.Error(emailEx, "[Invitation] Email send failed for {Email}; invitation row {InvitationId} persisted, signup URL available in logs", normalizedEmail, invitationId);
            Sentry.SentrySdk.CaptureException(emailEx);
        }

        return new InvitationDto(
            invitationId, normalizedEmail, role.ToString(), InvitationStatus.Pending.ToString(),
            inviterName, expiresAt, DateTime.UtcNow);
    }

    public async Task<InvitationInfoDto?> GetInfoAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        var invitation = await invitationProc.GetByTokenHashAsync(tokenHash);
        if (invitation is null) return null;

        var inviter = await businessUserProc.GetByIdAsync(invitation.InvitedByBusinessUserId);
        var inviterName = inviter is null ? "" : $"{inviter.FirstName} {inviter.LastName}".Trim();

        return new InvitationInfoDto(invitation.Email, invitation.Role.ToString(), inviterName, invitation.ExpiresAt);
    }

    public async Task<(BusinessUserDto User, string SessionToken, string Jwt)> AcceptAsync(
        string rawToken, string password, string? firstName, string? lastName,
        string? deviceName, string? ip)
    {
        var tokenHash = HashToken(rawToken);
        var invitation = await invitationProc.GetByTokenHashAsync(tokenHash)
            ?? throw new UnauthorizedAccessException("Invalid or expired invitation");

        if (await businessUserProc.ExistsByEmailAsync(invitation.Email))
            throw new InvalidOperationException("A user with this email already exists");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var emailHash = encryptionService.HashEmail(invitation.Email);

        var adminId = await businessUserProc.CreateAsync(
            invitation.Email, emailHash, (firstName ?? "Pending").Trim(), (lastName ?? "Setup").Trim(),
            passwordHash, invitation.Role.ToString());

        await invitationProc.AcceptAsync(invitation.Id);

        var sessionTokenBytes = RandomNumberGenerator.GetBytes(32);
        var sessionRawToken = Convert.ToBase64String(sessionTokenBytes);
        var sessionHash = HashToken(sessionRawToken);

        await businessUserProc.CreateDeviceSessionAsync(
            adminId, sessionHash, null, deviceName, ip,
            DateTime.UtcNow.AddDays(90));

        var admin = await businessUserProc.GetByIdAsync(adminId)
            ?? throw new InvalidOperationException("Business user creation failed");

        var dto = new BusinessUserDto(
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

        var jwt = await jwtService.GenerateAdminJwtAsync(admin);

        Log.Information("[Invitation] {Email} accepted invitation as {Role}", invitation.Email, invitation.Role);
        return (dto, sessionRawToken, jwt);
    }

    public async Task<List<InvitationDto>> ListAsync(Guid? invitedByBusinessUserId, int page, int pageSize)
    {
        var query = context.InvitationViews.AsNoTracking();

        if (invitedByBusinessUserId.HasValue)
            query = query.Where(i => i.InvitedByBusinessUserId == invitedByBusinessUserId.Value);

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvitationDto(
                i.InvitationId,
                i.Email,
                i.Role.ToString(),
                i.ExpiresAt < DateTime.UtcNow && i.Status == InvitationStatus.Pending
                    ? InvitationStatus.Expired.ToString()
                    : i.Status.ToString(),
                (i.InviterFirstName + " " + i.InviterLastName).Trim(),
                i.ExpiresAt,
                i.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task RevokeAsync(Guid invitationId, Guid businessUserId)
    {
        await invitationProc.RevokeAsync(invitationId);
        Log.Information("[Invitation] Invitation {Id} revoked by business user {BusinessUserId}", invitationId, businessUserId);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string BuildPortalUrl(string frontendUrl, AdminRole role)
    {
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var baseUri))
            return frontendUrl;

        var builder = new UriBuilder(baseUri);
        var isLocalhost = baseUri.IsLoopback
            || baseUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        if (isLocalhost)
        {
            builder.Port = role switch
            {
                AdminRole.Staff => 5175,
                AdminRole.Admin => 5174,
                AdminRole.Developer => 5176,
                _ => baseUri.IsDefaultPort ? -1 : baseUri.Port,
            };
        }
        else
        {
            var subdomain = role switch
            {
                AdminRole.Staff => "staff",
                AdminRole.Admin => "admin",
                AdminRole.Developer => "developer",
                _ => null,
            };
            if (subdomain is not null)
                builder.Host = $"{subdomain}.{baseUri.Host}";
        }

        return builder.Uri.GetLeftPart(UriPartial.Authority);
    }
}
