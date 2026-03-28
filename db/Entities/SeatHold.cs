namespace Db.Entities;

/// <summary>
/// Temporary hold on a seat for a specific event. Prevents other users from
/// selecting the same seat during checkout. Expires after configurable minutes
/// (default 10). Cleared by HoldCleanupWorker or on booking completion.
/// </summary>
public class SeatHold : BaseEntity
{
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TicketTypeId { get; set; }
    public TicketType TicketType { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
