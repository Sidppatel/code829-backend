namespace Contracts.DTOs.Tables;

public record LockTableRequest(
    Guid EventId,
    Guid TableId
);

public record ReleaseTableRequest(
    Guid EventId,
    Guid TableId
);

public record ReleaseBeaconRequest(
    Guid EventId,
    Guid TableId
)
{
    public Guid EventTableId => TableId;
};

public record CancelBeaconRequest(Guid PurchaseId);

public record TableLockDto(
    Guid TableId,
    string TableLabel,
    Guid EventId,
    Guid UserId,
    string Status,
    int Capacity,
    int PriceCents,
    int PlatformFeeCents,
    int DisplayPriceCents,
    DateTime ExpiresAt
);
