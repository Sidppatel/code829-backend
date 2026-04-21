using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class BusinessUserProcedures(EventPlatformDbContext context) : IBusinessUserProcedures
{
    public async Task<BusinessUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.BusinessUsers
            .FromSqlRaw("SELECT * FROM sp_get_business_user_by_id({0})", id)
            .Include(a => a.Image)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BusinessUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.BusinessUsers
            .FromSqlRaw("SELECT * FROM sp_get_business_user_by_email({0})", email)
            .Include(a => a.Image)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_business_user_exists_by_email({0}) AS \"Value\"", email)
            .FirstAsync(ct);
    }

    public async Task<Guid> CreateAsync(string email, string emailHash, string firstName, string lastName,
        string passwordHash, string role, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_business_user(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                email, emailHash, firstName, lastName, passwordHash, role)
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateAsync(Guid id, string? firstName = null, string? lastName = null, string? phone = null,
        string? role = null, bool? isActive = null, Guid? imageId = null, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_business_user(@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                [
                    new NpgsqlParameter("p0", id),
                    new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)firstName ?? DBNull.Value },
                    new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)lastName ?? DBNull.Value },
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)phone ?? DBNull.Value },
                    new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)role ?? DBNull.Value },
                    new NpgsqlParameter("p5", NpgsqlDbType.Boolean) { Value = (object?)isActive ?? DBNull.Value },
                    new NpgsqlParameter("p6", NpgsqlDbType.Uuid) { Value = (object?)imageId ?? DBNull.Value }
                ], ct);
    }

    public async Task<Guid?> SetBusinessUserImageAsync(Guid businessUserId, Guid imageId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid?>("SELECT sp_set_business_user_avatar_image({0}, {1}) AS \"Value\"", businessUserId, imageId)
            .FirstAsync(ct);
    }

    public async Task<Guid?> ClearBusinessUserImageAsync(Guid businessUserId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid?>("SELECT sp_clear_business_user_avatar_image({0}) AS \"Value\"", businessUserId)
            .FirstAsync(ct);
    }

    public async Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_business_user_password(@p0, @p1)",
                [id, passwordHash], ct);
    }

    public async Task UpdateLastLoginAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_business_user_last_login(@p0)",
                [id], ct);
    }

    public async Task IncrementFailedLoginAsync(Guid id, int maxAttempts, int lockoutMinutes, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_increment_business_user_failed_login(@p0, @p1, @p2)",
                [id, maxAttempts, lockoutMinutes], ct);
    }

    public async Task ResetLockoutAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_reset_business_user_lockout(@p0)",
                [id], ct);
    }

    public async Task<Guid> CreateDeviceSessionAsync(Guid businessUserId, string sessionHash,
        string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_business_user_device_session(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                new NpgsqlParameter("p0", businessUserId),
                new NpgsqlParameter("p1", sessionHash),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)fingerprint ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)deviceName ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value },
                new NpgsqlParameter("p5", expiresAt))
            .FirstAsync(ct);

        return result;
    }

    public async Task<int> RevokeAllSessionsAsync(Guid businessUserId, string? exceptHash = null, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_revoke_all_business_user_sessions(@p0, @p1) AS \"Value\"",
                new NpgsqlParameter("p0", businessUserId),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)exceptHash ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }
}
