using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public interface IAdminUserProcedures
{
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<Guid> CreateAsync(string email, string emailHash, string firstName, string lastName,
        string passwordHash, string role, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string? firstName = null, string? lastName = null, string? phone = null,
        string? role = null, bool? isActive = null, Guid? avatarImageId = null, CancellationToken ct = default);
    Task<Guid?> SetAdminUserAvatarImageAsync(Guid adminUserId, Guid imageId, CancellationToken ct = default);
    Task<Guid?> ClearAdminUserAvatarImageAsync(Guid adminUserId, CancellationToken ct = default);
    Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid id, CancellationToken ct = default);
    Task IncrementFailedLoginAsync(Guid id, int maxAttempts, int lockoutMinutes, CancellationToken ct = default);
    Task ResetLockoutAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateDeviceSessionAsync(Guid adminUserId, string sessionHash,
        string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default);
    Task<int> RevokeAllSessionsAsync(Guid adminUserId, string? exceptHash = null, CancellationToken ct = default);
}
