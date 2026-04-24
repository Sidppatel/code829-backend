using Contracts.Enums;
using Db;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Api.Services;

public class AuditLogService(EventPlatformDbContext context) : IAuditLogService
{
    public async Task<Guid> LogAsync(
        string eventType,
        AuditActorType actorType,
        Guid? actorId = null,
        string? subjectType = null,
        Guid? subjectId = null,
        string? action = null,
        string? metadataJson = null,
        string? ip = null,
        Guid? correlationId = null,
        CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_audit_log(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8) AS \"Value\"",
                new NpgsqlParameter("p0", eventType),
                new NpgsqlParameter("p1", actorType.ToString()),
                new NpgsqlParameter("p2", NpgsqlDbType.Uuid) { Value = (object?)actorId ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)subjectType ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Uuid) { Value = (object?)subjectId ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Text) { Value = (object?)(action ?? eventType) ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)metadataJson ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Uuid) { Value = (object?)correlationId ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }
}
