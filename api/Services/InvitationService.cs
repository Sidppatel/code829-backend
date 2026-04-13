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
    IEncryptionService encryptionService,
    IEmailService emailService,
    ISettingsService settingsService,
    IFileStorageService fileStorage,
    IJwtService jwtService
) : IInvitationService
{
    private const int InvitationExpiryDays = 7;

    public async Task<InvitationDto> CreateAsync(string email, AdminRole role, Guid invitedByAdminUserId)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        // Check if email already exists as an admin user
        if (await context.AdminUsers.AnyAsync(a => a.Email == normalizedEmail))
            throw new InvalidOperationException("A user with this email already exists");

        // Check for pending invitation to same email
        var existingPending = await context.Invitations
            .FirstOrDefaultAsync(i => i.Email == normalizedEmail && i.Status == InvitationStatus.Pending && i.ExpiresAt > DateTime.UtcNow);
        if (existingPending is not null)
            throw new InvalidOperationException("A pending invitation already exists for this email");

        var inviter = await context.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == invitedByAdminUserId)
            ?? throw new KeyNotFoundException("Inviter not found");

        // Generate token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashToken(rawToken);

        var invitation = new Invitation
        {
            Email = normalizedEmail,
            TokenHash = tokenHash,
            Role = role,
            InvitedByAdminUserId = invitedByAdminUserId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(InvitationExpiryDays)
        };

        context.Invitations.Add(invitation);
        await context.SaveChangesAsync();

        // Determine signup URL based on role
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

        await emailService.SendAsync(
            normalizedEmail,
            $"You're invited to join {appName}",
            EmailTemplates.Invitation(appName, inviterName, role.ToString(), signupUrl, InvitationExpiryDays)
        );

        Log.Information("[Invitation] {Inviter} invited {Email} as {Role}", inviterName, normalizedEmail, role);

        return MapDto(invitation, inviterName);
    }

    public async Task<InvitationInfoDto?> GetInfoAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        var invitation = await context.Invitations
            .Include(i => i.InvitedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash
                && i.Status == InvitationStatus.Pending
                && i.ExpiresAt > DateTime.UtcNow);

        if (invitation is null) return null;

        var inviterName = $"{invitation.InvitedBy.FirstName} {invitation.InvitedBy.LastName}".Trim();
        return new InvitationInfoDto(invitation.Email, invitation.Role.ToString(), inviterName, invitation.ExpiresAt);
    }

    public async Task<(AdminUserDto User, string SessionToken, string Jwt)> AcceptAsync(
        string rawToken, string password, string firstName, string lastName,
        string? deviceName, string? ip)
    {
        var tokenHash = HashToken(rawToken);
        var invitation = await context.Invitations
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash
                && i.Status == InvitationStatus.Pending
                && i.ExpiresAt > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException("Invalid or expired invitation");

        // Check email doesn't already exist
        if (await context.AdminUsers.AnyAsync(a => a.Email == invitation.Email))
            throw new InvalidOperationException("A user with this email already exists");

        // Create admin user
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var emailHash = encryptionService.HashEmail(invitation.Email);

        var adminId = await adminUserProc.CreateAsync(
            invitation.Email, emailHash, firstName.Trim(), lastName.Trim(),
            passwordHash, invitation.Role.ToString());

        // Mark invitation as accepted
        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Create device session
        var sessionTokenBytes = RandomNumberGenerator.GetBytes(32);
        var sessionRawToken = Convert.ToBase64String(sessionTokenBytes);
        var sessionHash = HashToken(sessionRawToken);

        await adminUserProc.CreateDeviceSessionAsync(
            adminId, sessionHash, null, deviceName, ip,
            DateTime.UtcNow.AddDays(90));

        // Load the created admin user for DTO
        var admin = await context.AdminUsers.AsNoTracking()
            .FirstAsync(a => a.Id == adminId);

        var dto = new AdminUserDto(
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

        var jwt = await jwtService.GenerateAdminJwtAsync(admin);

        Log.Information("[Invitation] {Email} accepted invitation as {Role}", invitation.Email, invitation.Role);
        return (dto, sessionRawToken, jwt);
    }

    public async Task<List<InvitationDto>> ListAsync(Guid? invitedByAdminUserId, int page, int pageSize)
    {
        var query = context.Invitations
            .Include(i => i.InvitedBy)
            .AsNoTracking()
            .AsQueryable();

        if (invitedByAdminUserId.HasValue)
            query = query.Where(i => i.InvitedByAdminUserId == invitedByAdminUserId.Value);

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvitationDto(
                i.Id,
                i.Email,
                i.Role.ToString(),
                i.ExpiresAt < DateTime.UtcNow && i.Status == InvitationStatus.Pending
                    ? InvitationStatus.Expired.ToString()
                    : i.Status.ToString(),
                (i.InvitedBy.FirstName + " " + i.InvitedBy.LastName).Trim(),
                i.ExpiresAt,
                i.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task RevokeAsync(Guid invitationId, Guid adminUserId)
    {
        var invitation = await context.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.Status == InvitationStatus.Pending)
            ?? throw new KeyNotFoundException("Invitation not found");

        invitation.Status = InvitationStatus.Revoked;
        await context.SaveChangesAsync();

        Log.Information("[Invitation] Invitation {Id} revoked by admin {AdminId}", invitationId, adminUserId);
    }

    private static InvitationDto MapDto(Invitation inv, string inviterName) => new(
        inv.Id, inv.Email, inv.Role.ToString(), inv.Status.ToString(),
        inviterName, inv.ExpiresAt, inv.CreatedAt);

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
