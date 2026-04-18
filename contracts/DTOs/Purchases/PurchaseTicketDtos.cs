namespace Contracts.DTOs.Purchases;

public record PurchaseTicketDto(
    Guid PurchaseTicketId,
    string TicketCode,
    int SeatNumber,
    string Status,
    Guid PurchaseId,
    string PurchaseNumber,
    Guid EventId,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    string? GuestName,
    string? GuestEmail,
    string? InvitedEmail,
    DateTime? InviteSentAt,
    DateTime? ClaimedAt,
    Guid? GuestUserId
);

/// <summary>
/// Stripped-down ticket view for guests — no payment info, no purchase owner details.
/// </summary>
public record GuestTicketDto(
    Guid PurchaseTicketId,
    string TicketCode,
    int SeatNumber,
    string Status,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    string PurchaseNumber,
    DateTime? ClaimedAt
);

public record InviteTicketRequest(string Email, string? GuestName);

public record ClaimTicketRequest(string Token);

public record TicketClaimInfoDto(
    Guid PurchaseTicketId,
    string TicketCode,
    int SeatNumber,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    string InviterName,
    bool AlreadyClaimed
);
