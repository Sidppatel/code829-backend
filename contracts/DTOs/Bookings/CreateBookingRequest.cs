namespace Contracts.DTOs.Bookings;

public record CreateBookingRequest(
    Guid EventId,
    List<BookingItemRequest> Items,
    Guid? TableId = null,
    Guid? TicketTypeId = null,
    int? SeatsReserved = null
);

public record BookingItemRequest(
    Guid TicketTypeId,
    Guid? SeatId = null
);
