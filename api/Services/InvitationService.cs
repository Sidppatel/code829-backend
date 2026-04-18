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
    Db.Repositories.StoredProcedures.IAdminUserProcedures adminUserProc,
    Db.Repositories.StoredProcedures.IInvitationProcedures invitationProc,
    IEncryptionService encryptionService,
    IEmailService emailService,
    ISettingsService settingsService,
    IFileStorageService fileStorage,
    IJwtService jwtService
) : IInvitationService
{
    private const double InvitationExpiryMinutes = 15;

    public async Task<InvitationDto> CreateAsync(string email, AdminRole role, Guid invitedByAdminUserId)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        if (await adminUserProc.ExistsByEmailAsync(normalizedEmail))
            throw new InvalidOperationException("A user with this email already exists");

        var existingPending = await invitationProc.GetPendingByEmailAsync(normalizedEmail);
        if (existingPending is not null)
            throw new InvalidOperationException("A pending invitation already exists for this email");

        var inviter = await adminUserProc.GetByIdAsync(invitedByAdminUserId)
            ?? throw new KeyNotFoundException("Inviter not found");

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var expiresAt = DateTime.UtcNow.AddMinutes(InvitationExpiryMinutes);
        var invitationId = await invitationProc.CreateAsync(
            normalizedEmail, tokenHash, role.ToString(), invitedByAdminUserId, expiresAt);

        var frontendUrl = await settingsService.GetOrDefaultAsync("frontend_url", "http://localhost:5173") ?? "http://localhost:5173";
        var subdomain = role switch
        {
            AdminRole.Staff => frontendUrl.Replace("://", "://staff.").Replace("localhost:5173", "localhost:5175"),
            AdminRole.Admin => frontendUrl.Replace("://", "://admin.").Replace("localhost:5173", "localhost:5174"),
            AdminRole.Developer => frontendUrl.Replace("://", "://developer.").Replace("localhost:5173", "localhost:5176"),
            _ => frontendUrl
        };
        var signupUrl = $"{subdomain}/signup?token={Uri.EscapeDataString(rawToken)}";

        var appName = await settingsService.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var inviterName = $"{inviter.FirstName} {inviter.LastName}".Trim();

        Log.Information("[Invitation] Sending invitation to {Email}. Signup URL: {Url}", normalizedEmail, signupUrl);

        await emailService.SendAsync(
            normalizedEmail,
            $"You're invited to join {appName}",
            EmailTemplates.Invitation(appName, inviterName, role.ToString(), signupUrl, (int)InvitationExpiryMinutes)
        );

        Log.Information("[Invitation] {Inviter} invited {Email} as {Role}", inviterName, normalizedEmail, role);

        return new InvitationDto(
            invitationId, normalizedEmail, role.ToString(), InvitationStatus.Pending.ToString(),
            inviterName, expiresAt, DateTime.UtcNow);
    }

    public async Task<InvitationInfoDto?> GetInfoAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        var invitation = await invitationProc.GetByTokenHashAsync(tokenHash);
        if (invitation is null) return null;

        var inviter = await adminUserProc.GetByIdAsync(invitation.InvitedByAdminUserId);
        var inviterName = inviter is null ? "" : $"{inviter.FirstName} {inviter.LastName}".Trim();

        return new InvitationInfoDto(invitation.Email, invitation.Role.ToString(), inviterName, invitation.ExpiresAt);
    }

    public async Task<(AdminUserDto User, string SessionToken, string Jwt)> AcceptAsync(
        string rawToken, string password, string? firstName, string? lastName,
        string? deviceName, string? ip)
    {
        var tokenHash = HashToken(rawToken);
        var invitation = await invitationProc.GetByTokenHashAsync(tokenHash)
            ?? throw new UnauthorizedAccessException("Invalid or expired invitation");

        if (await adminUserProc.ExistsByEmailAsync(invitation.Email))
            throw new InvalidOperationException("A user with this email already exists");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var emailHash = encryptionService.HashEmail(invitation.Email);

        var adminId = await adminUserProc.CreateAsync(
            invitation.Email, emailHash, (firstName ?? "Pending").Trim(), (lastName ?? "Setup").Trim(),
            passwordHash, invitation.Role.ToString());

        await invitationProc.AcceptAsync(invitation.Id);

        var sessionTokenBytes = RandomNumberGenerator.GetBytes(32);
        var sessionRawToken = Convert.ToBase64String(sessionTokenBytes);
        var sessionHash = HashToken(sessionRawToken);

        await adminUserProc.CreateDeviceSessionAsync(
            adminId, sessionHash, null, deviceName, ip,
            DateTime.UtcNow.AddDays(90));

        var admin = await adminUserProc.GetByIdAsync(adminId)
            ?? throw new InvalidOperationException("Admin user creation failed");

        var dto = new AdminUserDto(
            AdminUserId: admin.Id,
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

        var jwt = await jwtService.GenerateAdminJwtAsync(admin);

        Log.Information("[Invitation] {Email} accepted invitation as {Role}", invitation.Email, invitation.Role);
        return (dto, sessionRawToken, jwt);
    }

    public async Task<List<InvitationDto>> ListAsync(Guid? invitedByAdminUserId, int page, int pageSize)
    {
        var query = context.InvitationViews.AsNoTracking();

        if (invitedByAdminUserId.HasValue)
            query = query.Where(i => i.InvitedByAdminUserId == invitedByAdminUserId.Value);

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

    public async Task RevokeAsync(Guid invitationId, Guid adminUserId)
    {
        await invitationProc.RevokeAsync(invitationId);
        Log.Information("[Invitation] Invitation {Id} revoked by admin {AdminId}", invitationId, adminUserId);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
