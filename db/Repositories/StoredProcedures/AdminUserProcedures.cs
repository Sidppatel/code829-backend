using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class AdminUserProcedures(EventPlatformDbContext context) : IAdminUserProcedures
{
    public async Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.AdminUsers
            .FromSqlRaw("SELECT * FROM sp_get_admin_by_id({0})", id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.AdminUsers
            .FromSqlRaw("SELECT * FROM sp_get_admin_by_email({0})", email)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_admin_exists_by_email({0}) AS \"Value\"", email)
            .FirstAsync(ct);
    }

    public async Task<Guid> CreateAsync(string email, string emailHash, string firstName, string lastName,
        string passwordHash, string role, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_admin_user(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                email, emailHash, firstName, lastName, passwordHash, role)
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateAsync(Guid id, string? firstName = null, string? lastName = null, string? phone = null,
        string? role = null, bool? isActive = null, string? avatarPath = null, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_admin_user(@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                [
                    new NpgsqlParameter("p0", id),
                    new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)firstName ?? DBNull.Value },
                    new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)lastName ?? DBNull.Value },
                    new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)phone ?? DBNull.Value },
                    new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)role ?? DBNull.Value },
                    new NpgsqlParameter("p5", NpgsqlDbType.Boolean) { Value = (object?)isActive ?? DBNull.Value },
                    new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)avatarPath ?? DBNull.Value }
                ], ct);
    }

    public async Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_admin_password(@p0, @p1)",
                [id, passwordHash], ct);
    }

    public async Task UpdateLastLoginAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_admin_last_login(@p0)",
                [id], ct);
    }

    public async Task IncrementFailedLoginAsync(Guid id, int maxAttempts, int lockoutMinutes, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_increment_admin_failed_login(@p0, @p1, @p2)",
                [id, maxAttempts, lockoutMinutes], ct);
    }

    public async Task ResetLockoutAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_reset_admin_lockout(@p0)",
                [id], ct);
    }

    public async Task<Guid> CreateDeviceSessionAsync(Guid adminUserId, string sessionHash,
        string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_admin_device_session(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                new NpgsqlParameter("p0", adminUserId),
                new NpgsqlParameter("p1", sessionHash),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)fingerprint ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)deviceName ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value },
                new NpgsqlParameter("p5", expiresAt))
            .FirstAsync(ct);

        return result;
    }

    public async Task<int> RevokeAllSessionsAsync(Guid adminUserId, string? exceptHash = null, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_revoke_all_admin_sessions(@p0, @p1) AS \"Value\"",
                new NpgsqlParameter("p0", adminUserId),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)exceptHash ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }
}
