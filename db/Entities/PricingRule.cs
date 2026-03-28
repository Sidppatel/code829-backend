namespace Db.Entities;

/// <summary>
/// Optional per-event pricing overrides. When present, these override the
/// global platform_fee_percent / platform_fee_flat_cents settings.
/// </summary>
public class PricingRule : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int? FeePercent { get; set; }
    public int? FeeFlatCents { get; set; }
    public string? Description { get; set; }
}
