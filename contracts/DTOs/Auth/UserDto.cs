namespace Contracts.DTOs.Auth;

public record UserDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime CreatedAt,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Phone,
    bool OptInLocationEmail,
    bool HasCompletedOnboarding,
    string? ImageUrl,
    bool HasPassword,
    bool HasGoogle,
    bool EmailVerified
);
