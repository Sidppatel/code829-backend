namespace Db.Repositories.StoredProcedures;

public record BusinessPasswordResetTokenResult(
    Guid TokenId,
    Guid BusinessUserId,
    bool IsUsed,
    DateTime ExpiresAt,
    string? BusinessUserEmail);

public interface IBusinessPasswordResetTokenProcedures
{
    Task CreateAsync(Guid businessUserId, string tokenHash, DateTime expiresAt, string email, CancellationToken ct = default);
    Task<BusinessPasswordResetTokenResult?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task InvalidateAsync(string tokenHash, CancellationToken ct = default);
}
