namespace Contracts.DTOs.Auth;

public record AuthResponse(
    string Token,
    UserDto User,
    DateTime ExpiresAt
);
