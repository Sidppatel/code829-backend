namespace Db.Entities;

/// <summary>
/// An individual seat at a table. Seats are numbered within their table
/// (e.g., seat 1-8 at an 8-top). Each seat can be independently held/booked.
/// </summary>
public class Seat : BaseEntity
{
    public required string Label { get; set; }
    public int SeatNumber { get; set; }

    public Guid TableId { get; set; }
    public Table Table { get; set; } = null!;

    public ICollection<SeatHold> Holds { get; set; } = [];
}
