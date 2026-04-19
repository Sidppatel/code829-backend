using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class AdminPasswordResetTokenProcedures(EventPlatformDbContext context) : IAdminPasswordResetTokenProcedures
{
    public async Task CreateAsync(Guid adminUserId, string tokenHash, DateTime expiresAt, string email, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_create_admin_password_reset_token(@p0, @p1, @p2, @p3)",
                [adminUserId, tokenHash, expiresAt, email], ct);
    }

    public async Task<AdminPasswordResetTokenResult?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<AdminPasswordResetTokenResult>(
                "SELECT * FROM sp_get_admin_password_reset_token(@p0)",
                tokenHash)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async Task InvalidateAsync(string tokenHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_invalidate_admin_password_reset_token(@p0)",
                [tokenHash], ct);
    }
}
