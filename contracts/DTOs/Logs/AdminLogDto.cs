namespace Contracts.DTOs.Logs;

public record AdminLogDto(
    Guid Id,
    DateTime Timestamp,
    string Action,
    Guid? ActorId,
    string? ActorEmail,
    string? ActorRole,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    string? MetadataJson,
    string? IpAddress
);
