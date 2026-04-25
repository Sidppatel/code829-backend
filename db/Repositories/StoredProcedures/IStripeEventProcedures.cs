namespace Db.Repositories.StoredProcedures;

/// <summary>
/// SP wrappers for the Stripe Connect webhook tables (stripe_transfers,
/// stripe_payouts). Kept separate from <see cref="IStripeTransactionProcedures"/>
/// because those are transaction-side flows (PaymentIntents) while these are
/// payout-side mirrors written from the platform's perspective.
/// </summary>
public interface IStripeEventProcedures
{
    /// <summary>
    /// Idempotent insert of one Stripe transfer row. Resolves Organization +
    /// Purchase server-side so callers only need the Stripe-side identifiers.
    /// Returns the persisted row id (newly inserted or pre-existing).
    /// </summary>
    Task<Guid> InsertTransferAsync(
        string stripeTransferId,
        string stripeAccountId,
        string? paymentIntentId,
        int amountCents,
        string? currency,
        string rawEventJson,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts one payout row keyed by the Stripe payout id. Both
    /// <c>payout.created</c> and <c>payout.paid</c> route here — the SP
    /// preserves PaidAt across updates so the second event can't accidentally
    /// blank it out.
    /// </summary>
    Task<Guid> UpsertPayoutAsync(
        string stripePayoutId,
        string stripeAccountId,
        int amountCents,
        string? currency,
        string status,
        DateTime? arrivalDate,
        DateTime? paidAt,
        string rawEventJson,
        CancellationToken ct = default);
}
