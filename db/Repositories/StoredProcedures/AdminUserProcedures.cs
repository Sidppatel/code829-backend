using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class AdminUserProcedures(EventPlatformDbContext context) : IAdminUserProcedures
{
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
                    id,
                    (object?)firstName ?? DBNull.Value,
                    (object?)lastName ?? DBNull.Value,
                    (object?)phone ?? DBNull.Value,
                    (object?)role ?? DBNull.Value,
                    (object?)isActive ?? DBNull.Value,
                    (object?)avatarPath ?? DBNull.Value
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

    public async Task<Guid> CreateDeviceSessionAsync(Guid adminUserId, string sessionHash,
        string? fingerprint, string? deviceName, string? ip, DateTime expiresAt, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_admin_device_session(@p0, @p1, @p2, @p3, @p4, @p5) AS \"Value\"",
                adminUserId, sessionHash,
                (object?)fingerprint ?? DBNull.Value,
                (object?)deviceName ?? DBNull.Value,
                (object?)ip ?? DBNull.Value,
                expiresAt)
            .FirstAsync(ct);

        return result;
    }

    public async Task<int> RevokeAllSessionsAsync(Guid adminUserId, string? exceptHash = null, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_revoke_all_admin_sessions(@p0, @p1) AS \"Value\"",
                adminUserId, (object?)exceptHash ?? DBNull.Value)
            .FirstAsync(ct);

        return result;
    }
}
