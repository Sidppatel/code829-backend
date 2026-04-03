using Contracts.Enums;

namespace Db.Entities;

public class Booking : BaseEntity
{
    public required string BookingNumber { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int SubtotalCents { get; set; }
    public int FeeCents { get; set; }
    public int TotalCents { get; set; }

    /// <summary>
    /// QR token for check-in. Generated on payment confirmation.
    /// Stored as a unique string; the QR image is generated on-demand.
    /// </summary>
    public string? QrToken { get; set; }

    public string? Notes { get; set; }

    public Guid? TableId { get; set; }
    public Table? Table { get; set; }

    public int? SeatsReserved { get; set; }

    public ICollection<BookingItem> Items { get; set; } = [];
    public Payment? Payment { get; set; }
}
