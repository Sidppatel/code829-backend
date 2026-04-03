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
    int TotalBookings,
    int PaidBookings,
    int CheckedInBookings,
    int PendingBookings,
    int CancelledBookings,
    int RefundedBookings,
    long RevenueCents,
    long PotentialRevenueCents,
    int TotalCapacity,
    int SoldCount,
    List<RecentBookingDto> RecentBookings
);

public record RecentBookingDto(
    Guid Id,
    string BookingNumber,
    string UserName,
    string UserEmail,
    string Status,
    int TotalCents,
    DateTime CreatedAt
);
