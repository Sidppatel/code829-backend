namespace Contracts.DTOs.Auth;

public record AuthResponse(
    string Token,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime ExpiresAt,
    bool HasCompletedOnboarding
);
