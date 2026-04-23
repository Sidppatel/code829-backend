namespace Contracts.DTOs.Auth;

public record AdminLoginRequest(string Email, string Password);

public record BusinessUserDto(
    Guid BusinessUserId, string Email, string FirstName, string LastName,
    string Role, bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt,
    string? Phone, string? ImageUrl);

public record AdminAuthResponse(BusinessUserDto User);

public record CreateBusinessUserRequest(
    string Email, string FirstName, string LastName,
    string Password, string Role);

public record UpdateBusinessUserRequest(
    string? FirstName = null, string? LastName = null,
    string? Phone = null, string? Role = null, bool? IsActive = null);

public record ChangeBusinessUserPasswordRequest(string CurrentPassword, string NewPassword);

public record ResetBusinessUserPasswordRequest(string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);
