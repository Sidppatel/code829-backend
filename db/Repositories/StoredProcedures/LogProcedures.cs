using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class LogProcedures(EventPlatformDbContext context) : ILogProcedures
{
    public async Task<Guid> CreateAdminLogAsync(string action, Guid? actorId, string? actorEmail, string? actorRole, string? entityType, Guid? entityId, string? description, string? metadataJson, string? ip, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_admin_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8) AS \"Value\"",
                action,
                (object?)actorId ?? DBNull.Value, (object?)actorEmail ?? DBNull.Value,
                (object?)actorRole ?? DBNull.Value,
                (object?)entityType ?? DBNull.Value, (object?)entityId ?? DBNull.Value,
                (object?)description ?? DBNull.Value, (object?)metadataJson ?? DBNull.Value,
                (object?)ip ?? DBNull.Value)
            .FirstAsync(ct);

        return result;
    }

    public async Task<Guid> CreateDeveloperLogAsync(string severity, string message, string? exceptionType, string? stackTrace, string? requestPath, string? requestMethod, int? statusCode, Guid? userId, string? ip, string? correlationId, string? metadataJson, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_developer_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10) AS \"Value\"",
                severity, message,
                (object?)exceptionType ?? DBNull.Value, (object?)stackTrace ?? DBNull.Value,
                (object?)requestPath ?? DBNull.Value, (object?)requestMethod ?? DBNull.Value,
                (object?)statusCode ?? DBNull.Value, (object?)userId ?? DBNull.Value,
                (object?)ip ?? DBNull.Value, (object?)correlationId ?? DBNull.Value,
                (object?)metadataJson ?? DBNull.Value)
            .FirstAsync(ct);

        return result;
    }

    public async Task<Guid> CreateSystemLogAsync(string category, string action, string? source, string? entityType, Guid? entityId, string? beforeJson, string? afterJson, Guid? actorId, string? correlationId, int? durationMs, string? metadataJson, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_system_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10) AS \"Value\"",
                category, action,
                (object?)source ?? DBNull.Value, (object?)entityType ?? DBNull.Value,
                (object?)entityId ?? DBNull.Value, (object?)beforeJson ?? DBNull.Value,
                (object?)afterJson ?? DBNull.Value, (object?)actorId ?? DBNull.Value,
                (object?)correlationId ?? DBNull.Value, (object?)durationMs ?? DBNull.Value,
                (object?)metadataJson ?? DBNull.Value)
            .FirstAsync(ct);

        return result;
    }

    public async Task<Guid> CreateEmailLogAsync(string recipient, string subject, string? body, string status, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_email_log(@p0, @p1, @p2, @p3) AS \"Value\"",
                recipient, subject, (object?)body ?? DBNull.Value, status)
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
