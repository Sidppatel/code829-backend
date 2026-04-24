namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for PUT /developer/organizations/{id}. All fields nullable —
/// the underlying SP only updates non-null fields (COALESCE pattern).
/// </summary>
public record OrganizationUpdateRequest(
    string? Name,
    string? LegalName,
    string? CountryCode
);
