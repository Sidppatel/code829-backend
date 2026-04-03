namespace Contracts.DTOs.Events;

public record EventFeeResponse(
    Guid EventId,
    string Title,
    string LayoutMode,
    int? PlatformFeeCents,
    int DefaultFeeCents,
    List<TableTypeFee> TableTypes
);

public record TableTypeFee(
    Guid Id,
    string Label,
    int PriceCents,
    int? PlatformFeeCents
);

public record UpdateEventFeeRequest(int? PlatformFeeCents);

public record UpdateTableTypeFeesRequest(Dictionary<Guid, int?> TableTypeFees);
