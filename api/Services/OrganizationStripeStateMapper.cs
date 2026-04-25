namespace Api.Services;

/// <summary>
/// Pure function that derives the FE-facing state string for an Organization's
/// Stripe Connect account from the four boolean flags (plus disabled-reason)
/// that Stripe returns. Centralised so admin + developer status endpoints stay
/// in lock-step and the FE never has to re-implement the precedence rules.
///
/// <para>
/// <b>State precedence (top wins):</b>
/// <list type="number">
///   <item><c>not_started</c> — no Stripe account id on file.</item>
///   <item><c>rejected</c> — Stripe set <c>requirements.disabled_reason</c>
///         starting with <c>"rejected"</c> (terminal state — manual review).</item>
///   <item><c>identity_pending</c> — KYC details not yet submitted.</item>
///   <item><c>needs_bank</c> — identity complete, payouts not yet enabled
///         (typically waiting on external_account).</item>
///   <item><c>active</c> — both <c>charges_enabled</c> and
///         <c>payouts_enabled</c> are true.</item>
///   <item><c>identity_pending</c> — fallback for any other partial state.</item>
/// </list>
/// </para>
/// </summary>
public static class OrganizationStripeStateMapper
{
    /// <summary>
    /// Derives the state string. See class summary for precedence rules.
    /// </summary>
    /// <param name="stripeAccountId">Connected account id; null/empty means
    /// the org has not started onboarding.</param>
    /// <param name="detailsSubmitted"><c>account.details_submitted</c>.</param>
    /// <param name="chargesEnabled"><c>account.charges_enabled</c>.</param>
    /// <param name="payoutsEnabled"><c>account.payouts_enabled</c>.</param>
    /// <param name="disabledReason"><c>account.requirements.disabled_reason</c>;
    /// null when the account is not disabled.</param>
    public static string Derive(
        string? stripeAccountId,
        bool detailsSubmitted,
        bool chargesEnabled,
        bool payoutsEnabled,
        string? disabledReason)
    {
        if (string.IsNullOrEmpty(stripeAccountId)) return "not_started";
        if (!string.IsNullOrEmpty(disabledReason) && disabledReason.StartsWith("rejected", StringComparison.OrdinalIgnoreCase)) return "rejected";
        if (!detailsSubmitted) return "identity_pending";
        if (!payoutsEnabled) return "needs_bank";
        if (chargesEnabled && payoutsEnabled) return "active";
        return "identity_pending";
    }
}
