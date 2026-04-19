namespace Contracts.DTOs.Logs;

public record SystemLogDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid SystemLogId,
    DateTime Timestamp,
    string Category,
    string Action,
    string? Source,
    string? EntityType,
    Guid? EntityId,
    string? BeforeJson,
    string? AfterJson,
    Guid? UserId,
    string? UserEmail,
    string? UserRole,
    string? CorrelationId,
    long? DurationMs,
    string? MetadataJson
);
