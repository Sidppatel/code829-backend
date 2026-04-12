using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class UserProcedures(EventPlatformDbContext context) : IUserProcedures
{
    public async Task UpdateUserProfileAsync(Guid userId, string firstName, string lastName, string? phone, string? address, string? city, string? state, string? zip, bool optIn, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_profile(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                [userId, firstName, lastName,
                 (object?)phone ?? DBNull.Value,
                 (object?)address ?? DBNull.Value,
                 (object?)city ?? DBNull.Value,
                 (object?)state ?? DBNull.Value,
                 (object?)zip ?? DBNull.Value,
                 optIn], ct);
    }

    public async Task UpdateUserAvatarAsync(Guid userId, string avatarPath, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_avatar(@p0, @p1)",
                [userId, avatarPath], ct);
    }

    public async Task UpdateUserRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_role(@p0, @p1)",
                [userId, role], ct);
    }
}
