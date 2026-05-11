using Contracts.DTOs.Auth;

namespace Api.Services;

public interface IAuthService
{
    Task<MagicLinkResponse> SendMagicLinkAsync(string email, string? returnUrl = null, string? frontendOrigin = null);
    Task<(UserDto User, string SessionToken, string Jwt)> VerifyMagicLinkAsync(string token, string? deviceName, string? ip);
#if DEBUG
    Task<(UserDto User, string SessionToken, string Jwt)> DevLoginAsync(string email, string? deviceName, string? ip);
#endif
    Task<UserDto?> GetCurrentUserAsync(Guid userId);
    Task LogoutAsync(string sessionHash);
    Task<List<DeviceSessionDto>> GetSessionsAsync(Guid userId, string? currentSessionHash);
    Task RevokeSessionAsync(Guid sessionId, Guid userId);
    Task RevokeAllSessionsAsync(Guid userId, string? exceptSessionHash);

    Task<SignupResponse> SignupAsync(string email, string firstName, string lastName, string password, string? ip, string? frontendOrigin);
    Task<(UserDto User, string SessionToken, string Jwt)> SigninAsync(string email, string password, string? deviceName, string? ip);
    Task<(UserDto User, string SessionToken, string Jwt)> VerifyEmailAsync(string token, string? deviceName, string? ip);
    Task RequestPasswordResetAsync(string email, string? ip, string? frontendOrigin);
    Task ResetPasswordAsync(string token, string newPassword);
    Task<(UserDto User, string SessionToken, string Jwt)> SignInWithGoogleAsync(string? credential, string? code, string? deviceName, string? ip);
    Task SetOrChangePasswordAsync(Guid userId, string? currentPassword, string newPassword, string? currentSessionHash);
}
