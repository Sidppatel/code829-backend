namespace Db.Repositories.StoredProcedures;

public interface IPurchaseProcedures
{
    Task<Guid> CreatePurchaseAsync(Guid userId, Guid eventId, Guid? tableId, int? seats, Guid? eventTicketTypeId, int subtotalCents, int feeCents, int totalCents, string purchaseNumber, string status = "Pending", CancellationToken ct = default);
    Task<Guid> ReserveOpenCapacityAsync(Guid userId, Guid eventId, int seats, Guid? eventTicketTypeId, int subtotalCents, int feeCents, int totalCents, string purchaseNumber, CancellationToken ct = default);
    Task ConfirmPurchaseAsync(Guid purchaseId, string qrToken, CancellationToken ct = default);
    Task CancelPurchaseAsync(Guid purchaseId, CancellationToken ct = default);
    Task RefundPurchaseAsync(Guid purchaseId, CancellationToken ct = default);
}
