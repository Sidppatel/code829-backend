namespace Db.Entities;

/// <summary>
/// A line item in a booking — one per seat/ticket selected.
/// Each item gets its own unique QR code for individual check-in.
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

    /// <summary>Unique QR token for this specific seat/ticket. Generated on payment confirmation.</summary>
    public string? QrToken { get; set; }

    /// <summary>Optional guest name for this seat (for table bookings).</summary>
    public string? GuestName { get; set; }

    /// <summary>Optional guest email for this seat (for table bookings).</summary>
    public string? GuestEmail { get; set; }

    /// <summary>Secure token for sharing this seat's QR code via link (no login required).</summary>
    public string? InvitationToken { get; set; }

    /// <summary>Whether the invitation has been sent.</summary>
    public bool InvitationSent { get; set; }

    /// <summary>Whether this specific item has been checked in.</summary>
    public bool IsCheckedIn { get; set; }
}
