namespace Db.Entities.Views;

public class OrganizationView
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string CountryCode { get; set; } = "US";
    public string? StripeConnectedAccountId { get; set; }
    public bool StripeChargesEnabled { get; set; }
    public bool StripePayoutsEnabled { get; set; }
    public bool StripeDetailsSubmitted { get; set; }
    public DateTime? StripeOnboardedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    // Aggregates
    public int MemberCount { get; set; }
    public int EventCount { get; set; }
    public long TotalRevenueCents { get; set; }
}
