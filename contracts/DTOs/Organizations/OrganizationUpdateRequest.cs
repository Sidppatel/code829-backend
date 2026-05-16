namespace Contracts.DTOs.Organizations;

public record OrganizationUpdateRequest(
    string? Name,
    string? LegalName,
    string? CountryCode
);
