using Contracts.Enums;

namespace Api.Services;

public interface IAuditLogService
{
    Task<Guid> LogAsync(
        string eventType,
        AuditActorType actorType,
        Guid? actorId = null,
        string? subjectType = null,
        Guid? subjectId = null,
        string? action = null,
        string? metadataJson = null,
        string? ip = null,
        Guid? correlationId = null,
        CancellationToken ct = default);
}
