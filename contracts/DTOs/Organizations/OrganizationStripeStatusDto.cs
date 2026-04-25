namespace Contracts.DTOs.Organizations;

/// <summary>
/// Lightweight projection of an Organization member used by the Stripe status
/// endpoints so admin/developer dashboards can render the member roster
/// alongside Stripe Connect state without a second round trip.
/// </summary>
public record OrganizationMemberDto(
    Guid BusinessUserId,
    string Email,
    string? DisplayName
);

/// <summary>
/// Composite Stripe Connect status payload returned by both the admin
/// (<c>GET /admin/organizations/{id}/stripe/status</c>) and developer
/// (<c>GET /developer/organizations/{id}/stripe/status</c>) status endpoints.
///
/// <para>
/// Aggregates the live Stripe account snapshot, a derived state string the FE
/// can switch on without re-implementing the booleans-to-state machine, the
/// masked bank account last4, the org member roster, and an optional one-shot
/// Express dashboard URL (only populated when the caller explicitly requested
/// it — Stripe LoginLinks expire ~5 minutes after creation, never cache).
/// </para>
/// </summary>
/// <param name="OrganizationId">Local organization id.</param>
/// <param name="OrganizationName">Display name of the organization.</param>
/// <param name="StripeAccount">Live Stripe account snapshot, or <c>null</c>
/// when the org has no connected account yet.</param>
/// <param name="State">One of <c>"not_started"</c>, <c>"identity_pending"</c>,
/// <c>"needs_bank"</c>, <c>"active"</c>, <c>"rejected"</c>. Derived via
/// <c>OrganizationStripeStateMapper.Derive</c>.</param>
/// <param name="BankAccountLast4">Formatted as <c>"**** 4242"</c>, or
/// <c>null</c> when no external bank account is attached.</param>
/// <param name="Members">Organization member roster; empty list when the org
/// has no members.</param>
/// <param name="ExpressDashboardUrl"><c>null</c> unless the caller minted a
/// one-shot LoginLink and passed it in.</param>
/// <param name="FetchedAt">Server-side timestamp when this snapshot was
/// assembled — useful for FE staleness display.</param>
public record OrganizationStripeStatusDto(
    Guid OrganizationId,
    string OrganizationName,
    StripeAccountStatusDto? StripeAccount,
    string State,
    string? BankAccountLast4,
    List<OrganizationMemberDto> Members,
    string? ExpressDashboardUrl,
    DateTime FetchedAt
);
