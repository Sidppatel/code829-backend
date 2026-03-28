namespace Contracts.DTOs.Bookings;

public record CreateBookingRequest(
    Guid EventId,
    List<BookingItemRequest> Items
);

public record BookingItemRequest(
    Guid TicketTypeId,
    Guid? SeatId = null
);
