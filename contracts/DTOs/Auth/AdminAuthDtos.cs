namespace Contracts.DTOs.Auth;

public record AdminLoginRequest(string Email, string Password);

public record AdminUserDto(
    Guid AdminUserId, string Email, string FirstName, string LastName,
    string Role, bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt,
    string? Phone, string? ImageUrl);

public record AdminAuthResponse(AdminUserDto User, string? Token = null);

public record CreateAdminUserRequest(
    string Email, string FirstName, string LastName,
    string Password, string Role);

public record UpdateAdminUserRequest(
    string? FirstName = null, string? LastName = null,
    string? Phone = null, string? Role = null, bool? IsActive = null);

public record ChangeAdminPasswordRequest(string CurrentPassword, string NewPassword);

public record ResetAdminPasswordRequest(string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);
