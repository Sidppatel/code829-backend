namespace Db.Entities;

/// <summary>
/// A line item in a booking — one per seat/ticket selected.
/// </summary>
public class BookingItem : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid TicketTypeId { get; set; }
    public TicketType TicketType { get; set; } = null!;

    public Guid? SeatId { get; set; }
    public Seat? Seat { get; set; }

    public int PriceCents { get; set; }
}
