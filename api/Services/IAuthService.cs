using Contracts.DTOs.Auth;

namespace Api.Services;

public interface IAuthService
{
    Task<MagicLinkResponse> SendMagicLinkAsync(string email, string? returnUrl = null, string? frontendOrigin = null);
    Task<(UserDto User, string SessionToken)> VerifyMagicLinkAsync(string token, string? deviceName, string? ip);
    Task<(UserDto User, string SessionToken)> DevLoginAsync(string email, string? deviceName, string? ip);
    Task<UserDto?> GetCurrentUserAsync(Guid userId);
    Task LogoutAsync(string sessionHash);
    Task<List<DeviceSessionDto>> GetSessionsAsync(Guid userId, string? currentSessionHash);
    Task RevokeSessionAsync(Guid sessionId, Guid userId);
    Task RevokeAllSessionsAsync(Guid userId, string? exceptSessionHash);
}
