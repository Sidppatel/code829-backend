namespace Contracts.DTOs.Auth;

public record DeviceSessionDto(
    Guid Id,
    string? DeviceName,
    string? IpAddress,
    DateTime LastActivityAt,
    DateTime CreatedAt,
    bool IsCurrent
);
