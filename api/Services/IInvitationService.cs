using Contracts.DTOs.Auth;
using Contracts.Enums;

namespace Api.Services;

public interface IInvitationService
{
    Task<InvitationDto> CreateAsync(string email, AdminRole role, Guid invitedByBusinessUserId);
    Task<InvitationInfoDto?> GetInfoAsync(string rawToken);
    Task<(BusinessUserDto User, string SessionToken, string Jwt)> AcceptAsync(
        string rawToken, string password, string? firstName, string? lastName,
        string? deviceName, string? ip);
    Task<List<InvitationDto>> ListAsync(Guid? invitedByBusinessUserId, int page, int pageSize);
    Task RevokeAsync(Guid invitationId, Guid businessUserId);
}
