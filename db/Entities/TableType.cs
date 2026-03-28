using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// Reusable table type template. Global (not venue-scoped) — available across all events.
/// Used by layout editors to stamp out tables with consistent defaults.
/// </summary>
public class TableType : BaseEntity
{
    public required string Name { get; set; }
    public int DefaultCapacity { get; set; }
    public TableShape DefaultShape { get; set; } = TableShape.Round;
    public string? DefaultColor { get; set; }
    public int DefaultPriceCents { get; set; }
    public bool IsActive { get; set; } = true;

    // Keep VenueId nullable for backward compat (Phase 4 created venue-scoped types)
    public Guid? VenueId { get; set; }
    public Venue? Venue { get; set; }

    public ICollection<Table> Tables { get; set; } = [];
}
