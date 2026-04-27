using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class BusinessPasswordResetTokenProcedures(EventPlatformDbContext context) : IBusinessPasswordResetTokenProcedures
{
    public async Task CreateAsync(Guid businessUserId, string tokenHash, DateTime expiresAt, string email, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_create_business_user_password_reset_token(@p0, @p1, @p2, @p3)",
                [businessUserId, tokenHash, expiresAt, email], ct);
    }

    public async Task<BusinessPasswordResetTokenResult?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<BusinessPasswordResetTokenResult>(
                "SELECT * FROM sp_get_business_user_password_reset_token(@p0)",
                tokenHash)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async Task InvalidateAsync(string tokenHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_invalidate_business_user_password_reset_token(@p0)",
                [tokenHash], ct);
    }
}
