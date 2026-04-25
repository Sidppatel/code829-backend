namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for POST /developer/organizations/{id}/members.
/// </summary>
public record OrganizationMemberRequest(Guid BusinessUserId);
