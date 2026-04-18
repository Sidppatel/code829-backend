namespace Contracts.DTOs.Logs;

public record SystemLogDto(
    Guid Id,
    DateTime Timestamp,
    string Category,
    string Action,
    string? Source,
    string? EntityType,
    Guid? EntityId,
    string? BeforeJson,
    string? AfterJson,
    Guid? AdminUserId,
    string? CorrelationId,
    long? DurationMs,
    string? MetadataJson
);
