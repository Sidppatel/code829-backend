namespace Contracts.DTOs.Bookings;

public record BookingTicketDto(
    Guid Id,
    string TicketCode,
    int SeatNumber,
    string Status,
    Guid BookingId,
    string BookingNumber,
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
/// Stripped-down ticket view for guests — no payment info, no booking owner details.
/// </summary>
public record GuestTicketDto(
    Guid Id,
    string TicketCode,
    int SeatNumber,
    string Status,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    string BookingNumber,
    DateTime? ClaimedAt
);

public record InviteTicketRequest(string Email, string? GuestName);

public record ClaimTicketRequest(string Token);

public record TicketClaimInfoDto(
    Guid TicketId,
    string TicketCode,
    int SeatNumber,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    string InviterName,
    bool AlreadyClaimed
);
