using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class AuthProcedures(EventPlatformDbContext context) : IAuthProcedures
{
    public async Task<Guid> CreateMagicLinkAsync(string email, string tokenHash, DateTime expiresAt, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_magic_link(@p0, @p1, @p2) AS \"Value\"",
                email, tokenHash, expiresAt)
            .FirstAsync(ct);

        return result;
    }

    public async Task<MagicLinkResult?> ConsumeMagicLinkAsync(string tokenHash, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<MagicLinkResult>(
                "SELECT * FROM sp_consume_magic_link(@p0)",
                tokenHash)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async Task<Guid> UpsertUserAsync(string email, string emailHash, string firstName, string lastName, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_upsert_user(@p0, @p1, @p2, @p3) AS \"Value\"",
                email, emailHash, firstName, lastName)
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateUserLastLoginAsync(Guid userId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_user_last_login(@p0)",
                [userId], ct);
    }

    public async Task<Guid> CreateDeviceSessionAsync(Guid userId, string sessionHash, string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_device_session(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", sessionHash),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)fingerprint ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)deviceName ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value },
                new NpgsqlParameter("p5", expiresAt))
            .FirstAsync(ct);

        return result;
    }

    public async Task RevokeDeviceSessionAsync(string sessionHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_revoke_device_session(@p0)",
                [sessionHash], ct);
    }

    public async Task<int> RevokeAllUserSessionsAsync(Guid userId, string? exceptHash = null, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_revoke_all_user_sessions(@p0, @p1) AS \"Value\"",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)exceptHash ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }

    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<int>("SELECT sp_cleanup_expired_sessions() AS \"Value\"")
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdateSessionActivityAsync(string sessionHash, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_session_activity(@p0)",
                [sessionHash], ct);
    }
}
