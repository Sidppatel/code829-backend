namespace Contracts.DTOs.Organizations;

/// <summary>
/// Compact Organization row for list/grid endpoints. MemberCount is computed
/// server-side so the developer UI can render a count column without a per-row
/// roundtrip.
/// </summary>
public record OrganizationListItemDto(
    Guid Id,
    string Name,
    string? LegalName,
    string CountryCode,
    string? StripeConnectedAccountId,
    bool StripeChargesEnabled,
    bool StripePayoutsEnabled,
    bool StripeDetailsSubmitted,
    DateTime? StripeOnboardedAt,
    DateTime? ArchivedAt,
    int MemberCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>
    /// Coarse render state derived server-side via
    /// <c>OrganizationStripeStateMapper.Derive</c>. Single source of truth so
    /// list + detail endpoints stay in lock-step and the FE never has to
    /// re-implement the booleans-to-state machine.
    /// </summary>
    string StripeState
);
