namespace Contracts.DTOs.Auth;

public record SetPasswordRequest(string? CurrentPassword, string NewPassword);
