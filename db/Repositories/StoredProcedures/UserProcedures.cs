using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class UserProcedures(EventPlatformDbContext context) : IUserProcedures
{
    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_id({0})", userId)
            .Include(u => u.Address)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_email({0})", email)
            .Include(u => u.Address)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<User?> GetByEmailHashAsync(string emailHash, CancellationToken ct = default)
    {
        var user = await context.Users
            .FromSqlRaw("SELECT * FROM sp_get_user_by_email_hash({0})", emailHash)
            .Include(u => u.Address)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<List<User>> ListAsync(CancellationToken ct = default)
    {
        return await context.Users
            .FromSqlRaw("SELECT * FROM sp_list_users()")
            .Include(u => u.Address)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_user_exists_by_email({0}) AS \"Value\"", email)
            .FirstAsync(ct);
    }

    public async Task<UserCounts> GetCountsAsync(CancellationToken ct = default)
    {
        var row = await context.Database
            .SqlQueryRaw<UserCountsRow>("SELECT * FROM sp_user_counts()")
            .FirstAsync(ct);
        return new UserCounts(row.Total, row.Active, row.NewThisMonth);
    }

    public async Task UpdateUserProfileAsync(Guid userId, string? firstName, string? lastName, string? phone, string? address, string? city, string? state, string? zip, bool? optIn, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_profile(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                [
                    new NpgsqlParameter("p0", userId),
                    new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)firstName ?? DBNull.Value },
                    new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)lastName ?? DBNull.Value },
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)phone ?? DBNull.Value },
                    new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)address ?? DBNull.Value },
                    new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)city ?? DBNull.Value },
                    new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)state ?? DBNull.Value },
                    new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)zip ?? DBNull.Value },
                    new NpgsqlParameter("p8", NpgsqlDbType.Boolean) { Value = (object?)optIn ?? DBNull.Value }
                ], ct);
    }

    public async Task UpdateUserAvatarAsync(Guid userId, string avatarPath, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_avatar(@p0, @p1)",
                [userId, avatarPath], ct);
    }

    public async Task ClearUserAvatarAsync(Guid userId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_avatar(@p0, @p1)",
                [
                    new NpgsqlParameter("p0", userId),
                    new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = DBNull.Value }
                ], ct);
    }

    public async Task<bool> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_set_user_active({0}, {1}) AS \"Value\"", userId, isActive)
            .FirstAsync(ct);
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_delete_user({0}) AS \"Value\"", userId)
            .FirstAsync(ct);
    }

    private sealed class UserCountsRow
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int NewThisMonth { get; set; }
    }
}
