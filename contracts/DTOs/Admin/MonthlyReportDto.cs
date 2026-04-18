namespace Contracts.DTOs.Admin;

public record MonthlyReportDto(
    int Year,
    int Month,
    int TotalPurchases,
    long TotalChargedCents,
    long TotalAdminPayoutsCents,
    long TotalPlatformFeesCents,
    long TotalStripeFeesCents,
    long TotalTaxCollectedCents,
    long NetPlatformRevenueCents,
    List<EventMonthlyBreakdown> ByEvent
);

public record EventMonthlyBreakdown(
    Guid EventId,
    string EventTitle,
    int PurchaseCount,
    long ChargedCents,
    long AdminPayoutCents,
    long PlatformFeeCents,
    long StripeFeesCents,
    long TaxCollectedCents
);
