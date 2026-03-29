namespace Contracts.DTOs.Auth;

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string Role,
    DateTime CreatedAt,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Phone,
    bool OptInLocationEmail,
    bool HasCompletedOnboarding
);
