namespace Db.Repositories.StoredProcedures;

public record AdminPasswordResetTokenResult(
    Guid TokenId,
    Guid AdminUserId,
    bool IsUsed,
    DateTime ExpiresAt,
    string? AdminEmail);

public interface IAdminPasswordResetTokenProcedures
{
    Task CreateAsync(Guid adminUserId, string tokenHash, DateTime expiresAt, string email, CancellationToken ct = default);
    Task<AdminPasswordResetTokenResult?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task InvalidateAsync(string tokenHash, CancellationToken ct = default);
}
