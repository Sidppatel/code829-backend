namespace Contracts.DTOs.Auth;

public record CreateInvitationRequest(string Email, string Role);

public record AcceptInvitationRequest(string Token, string Password, string? FirstName = null, string? LastName = null);

public record InvitationDto(
    Guid InvitationId,
    string Email,
    string Role,
    string Status,
    string InvitedByName,
    DateTime ExpiresAt,
    DateTime CreatedAt);

public record InvitationInfoDto(
    string Email,
    string Role,
    string InvitedByName,
    DateTime ExpiresAt);
