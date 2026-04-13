namespace Db.Repositories.StoredProcedures;

public interface IAdminUserProcedures
{
    Task<Guid> CreateAsync(string email, string emailHash, string firstName, string lastName,
        string passwordHash, string role, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string? firstName = null, string? lastName = null, string? phone = null,
        string? role = null, bool? isActive = null, string? avatarPath = null, CancellationToken ct = default);
    Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateDeviceSessionAsync(Guid adminUserId, string sessionHash,
        string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default);
    Task<int> RevokeAllSessionsAsync(Guid adminUserId, string? exceptHash = null, CancellationToken ct = default);
}
