namespace Api.Services;

/// <summary>
/// Post-confirmation Stripe enrichment hook: pulls fee + tax data from Stripe,
/// writes back onto the local stripe_transactions row, and (when a tax
/// calculation exists) records the corresponding Stripe Tax Transaction so the
/// dashboard reports surface the line.
///
/// <para>
/// Two callers must invoke this for the data to land in dev as well as prod:
/// <list type="bullet">
///   <item><see cref="Api.Controllers.WebhooksController"/> on the
///         <c>payment_intent.succeeded</c> webhook (canonical Stripe path —
///         always fires in prod via /webhooks/stripe).</item>
///   <item><see cref="IPurchaseService.ConfirmAsync"/> after the FE-driven
///         confirm flow (covers dev where no public webhook URL is wired —
///         the dev workflow normally relies on
///         <c>stripe listen --forward-to localhost:8000/webhooks/stripe</c>,
///         but the in-band call lets the data land even without it).</item>
/// </list>
/// </para>
///
/// <para>Idempotent: each underlying SP either upserts or no-ops on conflict,
/// so dual-firing (webhook + confirm path) is safe.</para>
/// </summary>
public interface IPaymentEnrichmentService
{
    /// <summary>
    /// Fetches Stripe-side fees + tax-calculation totals, persists them onto
    /// stripe_transactions, and (when <paramref name="taxCalculationId"/> is
    /// present) creates the Stripe Tax Transaction + mirrors its id onto the
    /// PaymentIntent metadata so refund-time reversal can find it.
    /// All Stripe failures are caught + logged at Warning — never throws.
    /// </summary>
    Task EnrichAndRecordAsync(string paymentIntentId, string? taxCalculationId);
}
