namespace Contracts.DTOs.Seats;

public record HoldSeatRequest(
    Guid EventId,
    Guid SeatId,
    Guid TicketTypeId
);

public record HoldSeatsRequest(
    Guid EventId,
    List<SeatHoldItem> Seats
);

public record SeatHoldItem(
    Guid SeatId,
    Guid TicketTypeId
);

public record ReleaseSeatRequest(
    Guid EventId,
    Guid SeatId
);

public record SeatHoldDto(
    Guid Id,
    Guid SeatId,
    string SeatLabel,
    Guid EventId,
    Guid UserId,
    Guid TicketTypeId,
    string TicketTypeName,
    int PriceCents,
    DateTime ExpiresAt
);
