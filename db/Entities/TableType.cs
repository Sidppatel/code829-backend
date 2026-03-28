namespace Db.Entities;

/// <summary>
/// Defines a category of table (e.g., Round 8-top, Rectangle 4-top, Bar seating).
/// Used by the admin layout editor to stamp out tables of consistent shape/capacity.
/// </summary>
public class TableType : BaseEntity
{
    public required string Name { get; set; }
    public int SeatsPerTable { get; set; }
    public required string Shape { get; set; } // "round", "rectangle", "bar"
    public int WidthPx { get; set; } = 80;
    public int HeightPx { get; set; } = 80;

    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public ICollection<Table> Tables { get; set; } = [];
}
