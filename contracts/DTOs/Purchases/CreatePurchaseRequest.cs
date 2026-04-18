namespace Contracts.DTOs.Purchases;

public record CreatePurchaseRequest(
    Guid EventId,
    Guid? TableId = null,
    List<Guid>? TableIds = null,
    int? SeatsReserved = null,
    Guid? EventTicketTypeId = null
);
