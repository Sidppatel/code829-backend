using Contracts.DTOs.Auth;

namespace Api.Services;

public interface IAdminAuthService
{
    Task<(AdminUserDto User, string SessionToken)> LoginAsync(string email, string password, string? deviceName, string? ip);
    Task<AdminUserDto?> GetCurrentAdminAsync(Guid adminUserId);
    Task LogoutAsync(string sessionHash);
    Task<List<DeviceSessionDto>> GetSessionsAsync(Guid adminUserId, string? currentSessionHash);
    Task RevokeSessionAsync(Guid sessionId, Guid adminUserId);
    Task RevokeAllSessionsAsync(Guid adminUserId, string? exceptSessionHash);
    Task ChangePasswordAsync(Guid adminUserId, string currentPassword, string newPassword);
}
