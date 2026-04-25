namespace Db.Entities;

/// <summary>
/// Mirror of a Stripe Connect payout (connected account → organizer's bank).
/// One row per Stripe payout id. Upserted from <c>payout.created</c> and
/// <c>payout.paid</c> so the developer dashboard can render the payout history
/// per organization.
/// </summary>
public class StripePayout : BaseEntity
{
    /// <summary>Stripe-side <c>po_*</c> identifier. Unique — drives upsert.</summary>
    public required string StripePayoutId { get; set; }

    /// <summary>Owning Organization (the Stripe account the payout came from).</summary>
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";

    /// <summary>
    /// Stripe payout status. <c>pending|in_transit|paid|failed|canceled</c>.
    /// Free-text because Stripe can introduce new values without breaking us.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>Estimated date funds will arrive in the bank account.</summary>
    public DateTime? ArrivalDate { get; set; }

    /// <summary>Set when the payout flips to <c>paid</c>.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>Raw event payload (jsonb) for forensic / replay use.</summary>
    public string? RawEvent { get; set; }
}
