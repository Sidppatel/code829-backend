namespace Contracts.DTOs.Events;

public record TicketTypeDto(
    Guid Id,
    string Name,
    string? Description,
    int PriceCents,
    int QuantityTotal,
    int QuantitySold,
    int QuantityRemaining,
    int SortOrder
);
