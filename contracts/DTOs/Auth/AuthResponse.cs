namespace Contracts.DTOs.Auth;

public record AuthResponse(
    string Token,
    string Email,
    string Name,
    string Role,
    DateTime ExpiresAt,
    bool HasCompletedOnboarding
);
