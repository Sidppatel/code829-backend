namespace Contracts.DTOs.Logs;

public record AdminLogDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid AdminLogId,
    DateTime Timestamp,
    string Action,
    Guid? BusinessUserId,
    string? BusinessUserEmail,
    string? BusinessUserRole,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    string? MetadataJson,
    string? IpAddress
);
