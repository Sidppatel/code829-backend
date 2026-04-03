namespace Contracts.DTOs.Tables;

public record LockTableRequest(
    Guid EventId,
    Guid TableId,
    Guid TicketTypeId
);

public record ReleaseTableRequest(
    Guid EventId,
    Guid TableId
);

public record TableLockDto(
    Guid TableId,
    string TableLabel,
    Guid EventId,
    Guid UserId,
    string Status,
    int Capacity,
    string PriceType,
    int PriceCents,
    int PlatformFeeCents,
    DateTime ExpiresAt
);
