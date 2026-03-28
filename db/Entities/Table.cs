namespace Db.Entities;

/// <summary>
/// A physical table placed on the venue floor plan. Has an (x, y) position
/// for the canvas layout editor and a label (e.g., "T1", "A3").
/// </summary>
public class Table : BaseEntity
{
    public required string Label { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }

    public Guid TableTypeId { get; set; }
    public TableType TableType { get; set; } = null!;

    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public ICollection<Seat> Seats { get; set; } = [];
}
