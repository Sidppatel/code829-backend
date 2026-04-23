using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class EventProcedures(EventPlatformDbContext context) : IEventProcedures
{
    public async Task<Guid> CreateEventAsync(
        string? title, string? slug, string? description, string? status, string? category,
        DateTime? startDate, DateTime? endDate, string? imagePath, bool? isFeatured,
        string? layoutMode, int? maxCapacity, int? pricePerPersonCents,
        int? platformFeePercent, int? platformFeeCents,
        int? gridRows, int? gridCols, Guid? venueId, Guid? organizerId,
        DateTime? scheduledPublishAt)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_event(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18) AS \"Value\"",
                title!, slug!, description!, status!, category!,
                startDate!, endDate!, imagePath!, isFeatured!,
                layoutMode!, maxCapacity!, pricePerPersonCents!,
                platformFeePercent!, platformFeeCents!,
                gridRows!, gridCols!, venueId!, organizerId!,
                scheduledPublishAt!)
            .FirstAsync();

        return result;
    }

    public async Task UpdateEventAsync(
        Guid id, string? title, string? slug, string? description, string? category,
        DateTime? startDate, DateTime? endDate, string? imagePath, bool? isFeatured,
        string? layoutMode, int? maxCapacity, int? pricePerPersonCents,
        int? platformFeePercent, int? platformFeeCents,
        int? gridRows, int? gridCols, Guid? venueId, DateTime? scheduledPublishAt)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_event(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)",
            id, title!, slug!, description!, category!,
            startDate!, endDate!, imagePath!, isFeatured!,
            layoutMode!, maxCapacity!, pricePerPersonCents!,
            platformFeePercent!, platformFeeCents!,
            gridRows!, gridCols!, venueId!, scheduledPublishAt!);
    }

    public async Task ChangeEventStatusAsync(Guid id, string? status, DateTime? scheduledPublishAt)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_change_event_status(@p0, @p1, @p2)",
            id, status!, scheduledPublishAt!);
    }

    public async Task<IReadOnlyList<Guid>> PublishScheduledEventsAsync()
    {
        return await context.Database
            .SqlQueryRaw<Guid>("SELECT sp_publish_scheduled_events() AS \"Value\"")
            .ToListAsync();
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_delete_event({0})", [id], ct);
    }

    public async Task<EventStats?> GetEventStatsAsync(Guid id, CancellationToken ct = default)
    {
        var row = await context.Database
            .SqlQueryRaw<EventStatsRow>("SELECT * FROM sp_event_stats({0})", id)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new EventStats(row.TotalSold, row.MaxCapacity, row.FillRatePct, row.GrossRevenueCents);
    }

    public async Task<List<Guid>> SearchEventsAsync(string query, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>("SELECT \"EventId\" AS \"Value\" FROM sp_search_events({0})", query)
            .ToListAsync(ct);
    }

    private sealed class EventStatsRow
    {
        public int TotalSold { get; set; }
        public int MaxCapacity { get; set; }
        public int FillRatePct { get; set; }
        public long GrossRevenueCents { get; set; }
    }
}
