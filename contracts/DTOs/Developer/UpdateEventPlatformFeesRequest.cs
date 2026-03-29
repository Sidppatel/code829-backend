namespace Contracts.DTOs.Developer;

public record UpdateEventPlatformFeesRequest(
    List<TicketTypeFeeUpdate>? TicketFees,
    List<TableTypeFeeUpdate>? TableTypeFees
);

public record TicketTypeFeeUpdate(
    Guid TicketTypeId,
    int PlatformFeeCents
);

public record TableTypeFeeUpdate(
    Guid TableTypeId,
    int PlatformFeeCents
);
