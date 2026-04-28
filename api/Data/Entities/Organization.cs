namespace Db.Entities;

/// <summary>
/// A business entity that owns a Stripe Connect account and groups one or more
/// BusinessUsers (admins/staff). Multiple admins can share a single Organization
/// (and therefore a single Stripe payout destination), which is the primary
/// real-world case (e.g., 3-5 admins for one venue, all using the same bank).
/// </summary>
public class Organization : BaseEntity
{
    /// <summary>Display name (e.g., "The Lyric Theatre").</summary>
    public required string Name { get; set; }

    /// <summary>Legal entity name as registered with the tax authority. Optional.</summary>
    public string? LegalName { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code. Drives Stripe account country.</summary>
    public string CountryCode { get; set; } = "US";

    /// <summary>
    /// Stripe Connect account ID for this organization (e.g., "acct_xxx").
    /// One Stripe account per Organization. Required for receiving payouts via
    /// destination charges. Sparse-unique across active organizations.
    /// </summary>
    public string? StripeConnectedAccountId { get; set; }

    /// <summary>Mirrors Stripe's account.charges_enabled. Set via account.updated webhook.</summary>
    public bool StripeChargesEnabled { get; set; }

    /// <summary>Mirrors Stripe's account.payouts_enabled. Set via account.updated webhook.</summary>
    public bool StripePayoutsEnabled { get; set; }

    /// <summary>Mirrors Stripe's account.details_submitted. Flips true when onboarding completes.</summary>
    public bool StripeDetailsSubmitted { get; set; }

    /// <summary>Set the first time StripeDetailsSubmitted flips to true.</summary>
    public DateTime? StripeOnboardedAt { get; set; }

    /// <summary>
    /// JSON serialization of Stripe's requirements.currently_due array, captured
    /// from account.updated webhook. Drives "What's still needed?" UI.
    /// </summary>
    public string? StripeRequirementsDue { get; set; }

    /// <summary>Soft-delete flag. Archived organizations cannot host new events but retain audit history.</summary>
    public DateTime? ArchivedAt { get; set; }
}
