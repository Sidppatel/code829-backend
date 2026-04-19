using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class LogProcedures(EventPlatformDbContext context) : ILogProcedures
{
    public async Task<Guid> CreateAdminLogAsync(string action, Guid? adminUserId, string? entityType, Guid? entityId, string? description, string? metadataJson, string? ip, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_admin_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6) AS \"Value\"",
                new NpgsqlParameter("p0", action),
                new NpgsqlParameter("p1", NpgsqlDbType.Uuid) { Value = (object?)adminUserId ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)entityType ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Uuid) { Value = (object?)entityId ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)description ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)metadataJson ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }

    public async Task<Guid> CreateDeveloperLogAsync(string severity, string message, string? exceptionType, string? stackTrace, string? requestPath, string? requestMethod, int? statusCode, Guid? userId, string? ip, string? correlationId, string? metadataJson, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_developer_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10) AS \"Value\"",
                new NpgsqlParameter("p0", severity),
                new NpgsqlParameter("p1", message),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)exceptionType ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)stackTrace ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)requestPath ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)requestMethod ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Integer) { Value = (object?)statusCode ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Uuid) { Value = (object?)userId ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value },
                new NpgsqlParameter("p9", NpgsqlDbType.Text) { Value = (object?)correlationId ?? DBNull.Value },
                new NpgsqlParameter("p10", NpgsqlDbType.Text) { Value = (object?)metadataJson ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }

    public async Task<Guid> CreateSystemLogAsync(string category, string action, string? source, string? entityType, Guid? entityId, string? beforeJson, string? afterJson, Guid? userId, string? correlationId, int? durationMs, string? metadataJson, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_system_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10) AS \"Value\"",
                new NpgsqlParameter("p0", category),
                new NpgsqlParameter("p1", action),
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)source ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)entityType ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Uuid) { Value = (object?)entityId ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)beforeJson ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)afterJson ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Uuid) { Value = (object?)userId ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Text) { Value = (object?)correlationId ?? DBNull.Value },
                new NpgsqlParameter("p9", NpgsqlDbType.Integer) { Value = (object?)durationMs ?? DBNull.Value },
                new NpgsqlParameter("p10", NpgsqlDbType.Text) { Value = (object?)metadataJson ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }

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
