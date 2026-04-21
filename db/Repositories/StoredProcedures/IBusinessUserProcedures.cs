using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public interface IBusinessUserProcedures
{
    Task<BusinessUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BusinessUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<Guid> CreateAsync(string email, string emailHash, string firstName, string lastName,
        string passwordHash, string role, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string? firstName = null, string? lastName = null, string? phone = null,
        string? role = null, bool? isActive = null, Guid? imageId = null, CancellationToken ct = default);
    Task<Guid?> SetBusinessUserImageAsync(Guid businessUserId, Guid imageId, CancellationToken ct = default);
    Task<Guid?> ClearBusinessUserImageAsync(Guid businessUserId, CancellationToken ct = default);
    Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid id, CancellationToken ct = default);
    Task IncrementFailedLoginAsync(Guid id, int maxAttempts, int lockoutMinutes, CancellationToken ct = default);
    Task ResetLockoutAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateDeviceSessionAsync(Guid businessUserId, string sessionHash,
        string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default);
    Task<int> RevokeAllSessionsAsync(Guid businessUserId, string? exceptHash = null, CancellationToken ct = default);
}
