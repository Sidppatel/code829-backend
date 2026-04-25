namespace Contracts.DTOs.Organizations;

/// <summary>
/// Detail-endpoint variant of <see cref="OrganizationDto"/> that also surfaces
/// the BusinessUser roster. Returned by the developer single-org GET, update,
/// add-member, and remove-member endpoints so the FE can render the live
/// member list immediately after a mutation without an extra round trip.
///
/// The fields mirror <see cref="OrganizationDto"/> exactly; only
/// <see cref="Members"/> is additive. Reuses <see cref="OrganizationMemberDto"/>
/// from the Stripe status DTO to keep the member shape stable across endpoints.
/// </summary>
public record OrganizationDetailDto(
    Guid Id,
    string Name,
    string? LegalName,
    string CountryCode,
    string? StripeConnectedAccountId,
    bool StripeChargesEnabled,
    bool StripePayoutsEnabled,
    bool StripeDetailsSubmitted,
    DateTime? StripeOnboardedAt,
    string? StripeRequirementsDue,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrganizationMemberDto> Members
);
