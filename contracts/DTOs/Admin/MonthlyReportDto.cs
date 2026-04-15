namespace Contracts.DTOs.Admin;

public record MonthlyReportDto(
    int Year,
    int Month,
    int TotalBookings,
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
    int BookingCount,
    long ChargedCents,
    long AdminPayoutCents,
    long PlatformFeeCents,
    long StripeFeesCents,
    long TaxCollectedCents
);
