using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class UserProcedures(EventPlatformDbContext context) : IUserProcedures
{
    public async Task UpdateUserProfileAsync(Guid userId, string firstName, string lastName, string? phone, string? address, string? city, string? state, string? zip, bool optIn, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_profile(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                [
                    new NpgsqlParameter("p0", userId),
                    new NpgsqlParameter("p1", firstName),
                    new NpgsqlParameter("p2", lastName),
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)phone ?? DBNull.Value },
                    new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)address ?? DBNull.Value },
                    new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)city ?? DBNull.Value },
                    new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)state ?? DBNull.Value },
                    new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)zip ?? DBNull.Value },
                    new NpgsqlParameter("p8", optIn)
                ], ct);
    }

    public async Task UpdateUserAvatarAsync(Guid userId, string avatarPath, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_avatar(@p0, @p1)",
                [userId, avatarPath], ct);
    }

}
