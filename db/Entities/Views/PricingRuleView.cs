namespace Db.Entities.Views;

/// <summary>
/// Read-only view entity mapping to v_pricing_rules.
/// Resolves COALESCE defaults from pricing_rule_templates.
/// </summary>
public class PricingRuleView
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid? TableTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int PriceCents { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int? MaxCount { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int? FeePercent { get; set; }
    public int? FeeFlatCents { get; set; }
    public string? Description { get; set; }
    public Guid? TemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
