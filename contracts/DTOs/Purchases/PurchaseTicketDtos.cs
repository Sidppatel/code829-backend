namespace Contracts.DTOs.Purchases;

public record PurchaseTicketDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid PurchaseTicketId,
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
    Guid? GuestUserId,
    bool CanClaimSelf
);

public record GuestTicketDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid PurchaseTicketId,
    string TicketCode,
    int SeatNumber,
    string Status,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    DateTime? ClaimedAt
);

public record InviteTicketRequest(string Email, string? GuestName);

public record ClaimTicketRequest(string Token);

public record TicketClaimInfoDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid PurchaseTicketId,
    string TicketCode,
    int SeatNumber,
    string EventTitle,
    DateTime EventDate,
    string VenueName,
    string? TableLabel,
    bool AlreadyClaimed
);
