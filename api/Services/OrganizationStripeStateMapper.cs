namespace Api.Services;

public static class OrganizationStripeStateMapper
{
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
