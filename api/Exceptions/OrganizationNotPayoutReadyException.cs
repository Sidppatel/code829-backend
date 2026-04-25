namespace Api.Exceptions;

/// <summary>
/// Thrown by the purchase pipeline when Stripe Connect enforcement is on AND
/// the event organizer's Organization either has no Stripe account or hasn't
/// finished onboarding (charges_enabled=false). The PurchasesController
/// catches this and maps it to 409 Conflict — distinct from the generic
/// 400 fallback used for "your request is malformed" errors so the FE can
/// render the "ask the organizer to finish onboarding" empty state.
/// </summary>
public class OrganizationNotPayoutReadyException : Exception
{
    public OrganizationNotPayoutReadyException(string message) : base(message) { }
}
