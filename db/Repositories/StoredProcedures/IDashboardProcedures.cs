namespace Db.Repositories.StoredProcedures;

public interface IDashboardProcedures
{
    Task<NextEventDashboardRow?> GetNextEventDashboardAsync(DateTime nowUtc, CancellationToken ct = default);
    Task<List<EventRecentPurchaseRow>> GetEventRecentPurchasesAsync(Guid eventId, int limit, CancellationToken ct = default);
    Task<MonthlyReportSummaryRow> GetMonthlyReportSummaryAsync(int year, int month, CancellationToken ct = default);
    Task<List<MonthlyReportByEventRow>> GetMonthlyReportByEventAsync(int year, int month, CancellationToken ct = default);

    Task<List<PurchaseInfoForEventRow>> GetPurchaseInfoForEventAsync(Guid eventId, CancellationToken ct = default);
    Task<PurchaseStatsRow> GetPurchaseStatsAsync(Guid[]? coAdminIds, Guid? eventId, CancellationToken ct = default);
}

public record PurchaseInfoForEventRow(
    Guid TableId,
    int PurchaseCount,
    int SeatsBooked,
    long SubtotalCents
);

public record PurchaseStatsRow(
    int Total,
    int Paid,
    int CheckedIn,
    long Revenue
);

public record NextEventDashboardRow(
    Guid EventId,
    string Title,
    string Slug,
    string Status,
    string Category,
    DateTime StartDate,
    DateTime EndDate,
    string VenueName,
    string VenueAddress,
    string VenueCity,
    string VenueState,
    string? ImagePath,
    string LayoutMode,
    int DaysUntil,
    int TotalPurchases,
    int PaidPurchases,
    int CheckedInPurchases,
    int PendingPurchases,
    int CancelledPurchases,
    int RefundedPurchases,
    long RevenueCents,
    long PotentialRevenueCents,
    int TotalCapacity,
    int SoldCount
);

public record EventRecentPurchaseRow(
    Guid PurchaseId,
    string PurchaseNumber,
    string UserName,
    string UserEmail,
    string Status,
    int TotalCents,
    DateTime CreatedAt
);

public record MonthlyReportSummaryRow(
    int TotalPurchases,
    long TotalChargedCents,
    long TotalAdminPayoutsCents,
    long TotalPlatformFeesCents,
    long TotalStripeFeesCents,
    long TotalTaxCollectedCents,
    long NetPlatformRevenueCents
);

public record MonthlyReportByEventRow(
    Guid EventId,
    string EventTitle,
    int PurchaseCount,
    long ChargedCents,
    long AdminPayoutCents,
    long PlatformFeeCents,
    long StripeFeesCents,
    long TaxCollectedCents
);
