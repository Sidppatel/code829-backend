using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// A pricing rule for an event. Multiple rules can exist; the PricingEngine
/// resolves the lowest applicable price. Rules can be event-wide or scoped
/// to a specific table type (for Assigned Seating mode).
/// </summary>
public class PricingRule : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? TableTypeId { get; set; }
    public TableType? TableType { get; set; }

    public required string Name { get; set; }
    public PricingRuleType Type { get; set; } = PricingRuleType.Standard;
    public int PriceCents { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int? MaxCount { get; set; }
    public int UsedCount { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Legacy fields for backward compat with existing PricingEngine
    public int? FeePercent { get; set; }
    public int? FeeFlatCents { get; set; }
    public string? Description { get; set; }
}
