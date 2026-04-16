namespace Contracts.DTOs.Bookings;

public record CreateBookingRequest(
    Guid EventId,
    Guid? TableId = null,
    List<Guid>? TableIds = null,
    int? SeatsReserved = null,
    Guid? EventTicketTypeId = null
);
