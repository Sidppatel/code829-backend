namespace Api.Services;

/// <summary>
/// String constants for AppSetting keys read from the DB.
/// Lives here (not in Db) because AppSettings are an API-layer concept —
/// runtime behavior toggles, not schema.
///
/// Two reasons to add a key here:
///   1. The key is referenced from more than one call site (rename safety).
///   2. The key controls a feature flag whose default semantic ("off") matters
///      enough that documenting it next to the constant is worth the file.
///
/// Pure read-once configuration (e.g., <c>app_name</c>) doesn't need a constant —
/// keep this file lean.
/// </summary>
public static class SettingsKeys
{
    /// <summary>
    /// Master switch for Stripe Connect destination-charge enforcement.
    /// When <c>true</c>, every paid purchase requires the event organizer's
    /// Organization to have a fully onboarded Stripe Connect account
    /// (<c>StripeChargesEnabled = true</c>). When <c>false</c>, purchases for
    /// orgs without a Connect account fall back to platform-account charges
    /// (legacy behavior).
    ///
    /// <para>Default at seed time is <c>false</c> — we ship the feature dark
    /// and flip it on once enough organizers have completed onboarding.</para>
    /// </summary>
    public const string ConnectEnforcementEnabled = "connect_enforcement_enabled";
}
