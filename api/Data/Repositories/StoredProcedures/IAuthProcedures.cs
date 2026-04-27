namespace Db.Repositories.StoredProcedures;

public record MagicLinkResult(Guid Id, string Email, DateTime ExpiresAt);

public interface IAuthProcedures
{
    Task<Guid> CreateMagicLinkAsync(string email, string tokenHash, DateTime expiresAt, CancellationToken ct = default);
    Task<MagicLinkResult?> ConsumeMagicLinkAsync(string tokenHash, CancellationToken ct = default);
    Task<Guid> UpsertUserAsync(string email, string emailHash, string firstName, string lastName, CancellationToken ct = default);
    Task UpdateUserLastLoginAsync(Guid userId, CancellationToken ct = default);
    Task<Guid> CreateDeviceSessionAsync(Guid userId, string sessionHash, string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default);
    Task RevokeDeviceSessionAsync(string sessionHash, CancellationToken ct = default);
    Task<int> RevokeAllUserSessionsAsync(Guid userId, string? exceptHash = null, CancellationToken ct = default);
    Task<int> CleanupExpiredSessionsAsync(CancellationToken ct = default);
    Task UpdateSessionActivityAsync(string sessionHash, CancellationToken ct = default);
}
