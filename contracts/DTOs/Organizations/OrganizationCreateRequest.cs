namespace Contracts.DTOs.Organizations;

public record OrganizationCreateRequest(
    string Name,
    string? LegalName,
    string? CountryCode,
    Guid? InitialMemberBusinessUserId
);
