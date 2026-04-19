namespace Db.Repositories.StoredProcedures;

public interface ILogProcedures
{
    Task<Guid> CreateAdminLogAsync(string action, Guid? adminUserId, string? entityType, Guid? entityId, string? description, string? metadataJson, string? ip, CancellationToken ct = default);
    Task<Guid> CreateDeveloperLogAsync(string severity, string message, string? exceptionType, string? stackTrace, string? requestPath, string? requestMethod, int? statusCode, Guid? userId, string? ip, string? correlationId, string? metadataJson, CancellationToken ct = default);
    Task<Guid> CreateSystemLogAsync(string category, string action, string? source, string? entityType, Guid? entityId, string? beforeJson, string? afterJson, Guid? userId, string? correlationId, int? durationMs, string? metadataJson, CancellationToken ct = default);
    Task<Guid> CreateEmailLogAsync(string recipient, string subject, string? body, string status, CancellationToken ct = default);
    Task<int> CleanupOldLogsAsync(int devDays, int adminDays, int systemDays, CancellationToken ct = default);
}
