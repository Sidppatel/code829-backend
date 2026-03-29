using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// Reusable pricing rule template (e.g., "Early Bird -20%", "Standard").
/// Events reference these via PricingRule.TemplateId; NULL overrides fall back to template defaults.
/// </summary>
public class PricingRuleTemplate : BaseEntity
{
    public required string Name { get; set; }
    public PricingRuleType Type { get; set; } = PricingRuleType.Standard;
    public int DefaultPriceCents { get; set; }
    public int? DefaultFeePercent { get; set; }
    public int? DefaultFeeFlatCents { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<PricingRule> PricingRules { get; set; } = [];
}
