using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// A default floor plan for a venue. Venues can have multiple layouts (e.g., "Main Hall Grid", "Patio Setup").
/// Events reference a VenueLayout to inherit grid dimensions and default table placements.
/// </summary>
public class VenueLayout : BaseEntity
{
    public required string Name { get; set; }
    public LayoutMode LayoutMode { get; set; } = LayoutMode.None;
    public EditorMode? EditorMode { get; set; }
    public int? GridRows { get; set; }
    public int? GridCols { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public ICollection<VenueLayoutTable> Tables { get; set; } = [];
    public ICollection<Event> Events { get; set; } = [];
}
