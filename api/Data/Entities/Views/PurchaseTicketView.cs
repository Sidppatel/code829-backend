namespace Db.Entities.Views;

public class PurchaseTicketView
{
    public Guid PurchaseTicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string QrToken { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string? InvitedEmail { get; set; }
    public DateTime? InviteSentAt { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public Guid PurchaseId { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public string PurchaseStatus { get; set; } = string.Empty;

    public Guid? GuestUserId { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestFirstName { get; set; }
    public string? GuestLastName { get; set; }

    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }

    public string VenueName { get; set; } = string.Empty;
    public string VenueCity { get; set; } = string.Empty;

    public Guid PurchaseUserId { get; set; }
    public string PurchaseUserEmail { get; set; } = string.Empty;
    public string PurchaseUserFirstName { get; set; } = string.Empty;
    public string PurchaseUserLastName { get; set; } = string.Empty;

    public string? InviteTokenHash { get; set; }

    public Guid? PurchaseTableId { get; set; }
}
