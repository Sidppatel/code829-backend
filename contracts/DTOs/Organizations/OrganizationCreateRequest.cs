namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for POST /developer/organizations. CountryCode defaults to "US" if
/// omitted. InitialMemberBusinessUserId is optional — when null, the org is
/// created with no members and a developer can attach admins later.
/// </summary>
public record OrganizationCreateRequest(
    string Name,
    string? LegalName,
    string? CountryCode,
    Guid? InitialMemberBusinessUserId
);
