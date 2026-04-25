namespace Contracts.DTOs.Organizations;

/// <summary>
/// Full Organization DTO returned by detail endpoints. Includes Stripe Connect
/// status fields so admin/developer dashboards can render the right UI state
/// without a second round trip.
/// </summary>
public record OrganizationDto(
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
    DateTime UpdatedAt
);
