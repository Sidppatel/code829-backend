namespace Contracts.DTOs.Admin;

public record DashboardStatsDto(
    int TotalEvents,
    int PublishedEvents,
    int TotalPurchases,
    int PaidPurchases,
    int CheckedInPurchases,
    long TotalRevenueCents,
    int TotalUsers,
    int TotalVenues,
    List<EventRevenueDto> TopEvents,
    Dictionary<string, int> PurchasesByStatus,
    Dictionary<string, int> EventsByCategory
);

public record EventRevenueDto(
    Guid EventId,
    string Title,
    int PurchaseCount,
    long RevenueCents
);

public record NextEventDashboardDto(
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
    int SoldCount,
    List<RecentPurchaseDto> RecentPurchases
);

public record RecentPurchaseDto(
    Guid PurchaseId,
    string PurchaseNumber,
    string UserName,
    string UserEmail,
    string Status,
    int TotalCents,
    DateTime CreatedAt
);
