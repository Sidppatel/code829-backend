namespace Contracts.DTOs.Auth;

public record DeviceSessionDto(
    Guid DeviceSessionId,
    string? DeviceName,
    string? IpAddress,
    DateTime LastActivityAt,
    DateTime CreatedAt,
    bool IsCurrent
);
