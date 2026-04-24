using Npgsql;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Shared raw-SQL seed helpers for integration tests. tests/ is whitelisted from
/// the Data Access Rule (see CLAUDE.md), so direct INSERTs are allowed here.
/// </summary>
public static class TestSeed
{
    public record EventOptions(
        string LayoutMode = "Open",
        int? MaxCapacity = 100,
        string Status = "Published",
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        DateTime? ScheduledPublishAt = null);

    public static async Task<Guid> SeedBusinessUserAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO business_users ("Id","Email","FirstName","LastName","Role","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Test', 'Admin', 'Admin', true, now(), now())
            """,
            ("id", id), ("email", $"admin-{id}@test.com"));
        return id;
    }

    public static async Task<Guid> SeedVenueAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO venues ("Id","Name","Address","City","State","ZipCode","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, 'Test Venue', '1 Test St', 'Testville', 'TS', '00000', true, now(), now())
            """,
            ("id", id));
        return id;
    }

    public static async Task<Guid> SeedEventAsync(DatabaseFixture db, EventOptions? opts = null)
    {
        opts ??= new EventOptions();
        var venueId = await SeedVenueAsync(db);
        var businessUserId = await SeedBusinessUserAsync(db);

        var id = Guid.NewGuid();
        var start = opts.StartDate ?? DateTime.UtcNow.AddDays(7);
        var end = opts.EndDate ?? DateTime.UtcNow.AddDays(8);
        var publishedAt = opts.Status == "Published" ? (DateTime?)DateTime.UtcNow.AddMinutes(-5) : null;

        await db.ExecuteSqlAsync("""
            INSERT INTO events (
                "Id","Title","Slug","Description","Status","StartDate","EndDate",
                "IsFeatured","LayoutMode","MaxCapacity","PublishedAt","ScheduledPublishAt",
                "VenueId","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@id, @title, @slug, 'test', @status, @start, @end,
                false, @layout, @maxCap, @publishedAt, @scheduledAt,
                @venue, @business, now(), now())
            """,
            ("id", id),
            ("title", $"Test Event {id}"),
            ("slug", $"test-event-{id}"),
            ("status", opts.Status),
            ("start", start),
            ("end", end),
            ("layout", opts.LayoutMode),
            ("maxCap", (object?)opts.MaxCapacity ?? DBNull.Value),
            ("publishedAt", (object?)publishedAt ?? DBNull.Value),
            ("scheduledAt", (object?)opts.ScheduledPublishAt ?? DBNull.Value),
            ("venue", venueId),
            ("business", businessUserId));
        return id;
    }

    public static async Task<Guid> SeedUserAsync(DatabaseFixture db)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO users ("Id","Email","FirstName","LastName","Role","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Test', 'User', 'User', true, now(), now())
            """,
            ("id", id), ("email", $"user-{id}@test.com"));
        return id;
    }
}
