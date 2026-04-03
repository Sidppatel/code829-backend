namespace Contracts.DTOs.Bookings;

public record CreateBookingRequest(
    Guid EventId,
    Guid? TableId = null,
    int? SeatsReserved = null
);
