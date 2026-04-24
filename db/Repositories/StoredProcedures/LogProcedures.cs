using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class LogProcedures(EventPlatformDbContext context) : ILogProcedures
{
    public async Task<Guid> CreateEmailLogAsync(string recipient, string subject, string? body, string status, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_email_log(@p0, @p1, @p2, @p3) AS \"Value\"",
                new NpgsqlParameter("p0", recipient),
                new NpgsqlParameter("p1", subject),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)body ?? DBNull.Value },
                new NpgsqlParameter("p3", status))
            .FirstAsync(ct);

        return result;
    }

    public async Task<int> CleanupOldLogsAsync(int devDays, int adminDays, int systemDays, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<int>(
                "SELECT sp_cleanup_old_logs(@p0, @p1, @p2) AS \"Value\"",
                devDays, adminDays, systemDays)
            .FirstAsync(ct);

        return result;
    }
}
