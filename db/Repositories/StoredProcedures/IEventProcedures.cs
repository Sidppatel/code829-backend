namespace Db.Repositories.StoredProcedures;

public record EventStats(int TotalSold, int MaxCapacity, int FillRatePct, long GrossRevenueCents);

public interface IEventProcedures
{
    Task<Guid> CreateEventAsync(
        string? title, string? slug, string? description, string? status, string? category,
        DateTime? startDate, DateTime? endDate, string? imagePath, bool? isFeatured,
        string? layoutMode, int? maxCapacity, int? pricePerPersonCents,
        int? platformFeePercent, int? platformFeeCents,
        int? gridRows, int? gridCols, Guid? venueId, Guid? organizerId,
        DateTime? scheduledPublishAt);

    Task UpdateEventAsync(
        Guid id, string? title, string? slug, string? description, string? category,
        DateTime? startDate, DateTime? endDate, string? imagePath, bool? isFeatured,
        string? layoutMode, int? maxCapacity, int? pricePerPersonCents,
        int? platformFeePercent, int? platformFeeCents,
        int? gridRows, int? gridCols, Guid? venueId, DateTime? scheduledPublishAt);

    Task ChangeEventStatusAsync(Guid id, string? status, DateTime? scheduledPublishAt);

    Task<IReadOnlyList<Guid>> PublishScheduledEventsAsync();

    Task DeleteEventAsync(Guid id, CancellationToken ct = default);

    Task<EventStats?> GetEventStatsAsync(Guid id, CancellationToken ct = default);

    Task<List<Guid>> SearchEventsAsync(string query, CancellationToken ct = default);
}
