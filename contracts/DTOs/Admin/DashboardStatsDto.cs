namespace Contracts.DTOs.Admin;

public record DashboardStatsDto(
    int TotalEvents,
    int PublishedEvents,
    int TotalBookings,
    int PaidBookings,
    int CheckedInBookings,
    long TotalRevenueCents,
    int TotalUsers,
    int TotalVenues,
    List<EventRevenueDto> TopEvents,
    Dictionary<string, int> BookingsByStatus,
    Dictionary<string, int> EventsByCategory
);

public record EventRevenueDto(
    Guid EventId,
    string Title,
    int BookingCount,
    long RevenueCents
);
