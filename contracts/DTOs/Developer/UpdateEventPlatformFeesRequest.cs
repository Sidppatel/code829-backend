namespace Contracts.DTOs.Developer;

public record UpdateEventPlatformFeesRequest(
    List<TicketTypeFeeUpdate>? TicketFees,
    List<TableFeeUpdate>? TableFees
);

public record TicketTypeFeeUpdate(
    Guid TicketTypeId,
    int PlatformFeeCents
);

public record TableFeeUpdate(
    Guid TableId,
    int PlatformFeeCents
);
