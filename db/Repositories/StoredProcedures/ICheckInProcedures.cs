namespace Db.Repositories.StoredProcedures;

public record CheckInScanResult(
    bool Success,
    string Message,
    string? PurchaseNumber,
    string? GuestName,
    string? EventTitle,
    string? StatusStr,
    DateTime? CheckedInAt);

public interface ICheckInProcedures
{
    Task<CheckInScanResult?> ScanTicketAsync(string qrToken, CancellationToken ct = default);
    Task<CheckInScanResult?> ScanPurchaseAsync(string qrToken, CancellationToken ct = default);
    Task<DateTime?> GetEventLastCheckinAsync(Guid eventId, CancellationToken ct = default);
}
