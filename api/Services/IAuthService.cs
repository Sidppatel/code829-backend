using Contracts.DTOs.Auth;

namespace Api.Services;

public interface IAuthService
{
    Task<MagicLinkResponse> SendMagicLinkAsync(string email, string? returnUrl = null, string? frontendOrigin = null);
    Task<AuthResponse> VerifyMagicLinkAsync(string token);
    Task<AuthResponse> DevLoginAsync(string email);
    Task<UserDto?> GetCurrentUserAsync(Guid userId);
}
