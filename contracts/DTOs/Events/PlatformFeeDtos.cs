namespace Contracts.DTOs.Events;

public record EventFeeResponse(
    Guid EventId,
    string Title,
    string LayoutMode,
    int? PricePerPersonCents,
    int? MaxCapacity,
    int DefaultFeeCents,
    List<TableTypeFee> TableTypes,
    List<TicketTypeFee> TicketTypes
);

public record TableTypeFee(
    Guid Id,
    string Label,
    int PriceCents,
    int? PlatformFeeCents,
    bool IsLocked
);

public record TicketTypeFee(
    Guid Id,
    string Label,
    int PriceCents,
    int? PlatformFeeCents,
    bool IsLocked
);

public record UpdateTableTypeFeesRequest(Dictionary<Guid, int?> TableTypeFees);

public record UpdateTicketTypeFeesRequest(Dictionary<Guid, int?> TicketTypeFees);
