namespace Contracts.DTOs.Auth;

public record AuthResponse(
    UserDto User,
    string? Token = null
);
