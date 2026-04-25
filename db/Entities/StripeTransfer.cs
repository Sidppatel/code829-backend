namespace Db.Entities;

/// <summary>
/// Append-only record of a Stripe Connect transfer (platform → connected
/// account). Inserted from the <c>transfer.created</c> webhook so the
/// developer dashboard can reconcile platform fees against organizer payouts
/// without round-tripping Stripe on every read.
/// </summary>
public class StripeTransfer : BaseEntity
{
    /// <summary>
    /// The Stripe-side <c>tr_*</c> identifier. Unique on our side so retries of
    /// the same webhook are idempotent (the SP does <c>ON CONFLICT DO NOTHING</c>).
    /// </summary>
    public required string StripeTransferId { get; set; }

    /// <summary>FK to the Organization that received the transfer.</summary>
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// FK to the Purchase the transfer was associated with. Resolved from the
    /// PaymentIntent's <c>purchase_number</c> metadata at handler time. Nullable
    /// because Stripe transfers can occasionally arrive before we've persisted
    /// the StripeTransaction (rare, but the row still has audit value).
    /// </summary>
    public Guid? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";

    /// <summary>Raw event payload (jsonb) for forensic / replay use.</summary>
    public string? RawEvent { get; set; }
}
