namespace Contracts.DTOs.Organizations;

/// <summary>
/// Live Stripe account status snapshot returned by status endpoints.
/// Mirrors the shape of <c>StripeAccountStatus</c> in the StripeConnectService
/// but lives in contracts so the FE can type against it without dragging in
/// the api project.
/// </summary>
public record StripeAccountStatusDto(
    string AccountId,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    IReadOnlyList<string> RequirementsCurrentlyDue
);
