namespace Contracts.DTOs.Logs;

public record AdminLogDto(
    Guid AdminLogId,
    DateTime Timestamp,
    string Action,
    Guid? AdminUserId,
    string? AdminEmail,
    string? AdminRole,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    string? MetadataJson,
    string? IpAddress
);
