namespace Contracts.DTOs.Organizations;

/// <summary>
/// Body for POST /developer/organizations/{id}/stripe-account.
///
/// All fields optional — server applies defaults from the Organization row
/// (LegalName ?? Name) and constants (Mcc=7922 Theatrical/Ticket Agencies,
/// ProductDescription auto-templated). Pre-filling these collapses Stripe's
/// hosted "Business details" onboarding screen so the organizer sees only
/// Identity (KYC) + Bank Account.
///
/// BusinessType is required because it changes the Stripe onboarding
/// questionnaire shape (sole-trader vs registered company).
/// </summary>
public record StartStripeOnboardingRequest(
    string BusinessType,
    string? LegalName = null,
    string? ProductDescription = null,
    string? Mcc = null);
