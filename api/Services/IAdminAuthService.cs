using Contracts.DTOs.Auth;

namespace Api.Services;

public interface IAdminAuthService
{
    Task<(BusinessUserDto User, string SessionToken, string Jwt)> LoginAsync(string email, string password, string? deviceName, string? ip);
    Task<BusinessUserDto?> GetCurrentAdminAsync(Guid businessUserId);
    Task LogoutAsync(string sessionHash);
    Task<List<DeviceSessionDto>> GetSessionsAsync(Guid businessUserId, string? currentSessionHash);
    Task RevokeSessionAsync(Guid sessionId, Guid businessUserId);
    Task RevokeAllSessionsAsync(Guid businessUserId, string? exceptSessionHash);
    Task ChangePasswordAsync(Guid businessUserId, string currentPassword, string newPassword);
    Task RequestPasswordResetAsync(string email, string? origin);
    Task ResetPasswordAsync(string token, string newPassword);
}
