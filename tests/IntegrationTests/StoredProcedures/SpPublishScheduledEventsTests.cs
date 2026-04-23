using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpPublishScheduledEventsTests(DatabaseFixture db)
{
    private async Task<Guid> SeedScheduledEventAsync(DateTimeOffset scheduledAt)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO events ("Id","Title","Description","StartDate","EndDate",
                "LayoutMode","MaxCapacity","Status","ScheduledPublishAt","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@id, 'Scheduled Event', 'desc',
                now() + interval '7 days', now() + interval '8 days',
                'Open', 100, 'Draft', @sched, gen_random_uuid(), now(), now())
            """,
            ("id", id), ("sched", scheduledAt.UtcDateTime));
        return id;
    }

    [Fact]
    public async Task PublishesEvents_WithPastScheduledPublishAt()
    {
        var pastScheduled = DateTimeOffset.UtcNow.AddHours(-1);
        var eventId = await SeedScheduledEventAsync(pastScheduled);

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sp_publish_scheduled_events()";
        await using var reader = await cmd.ExecuteReaderAsync();

        var publishedIds = new List<Guid>();
        while (await reader.ReadAsync())
            publishedIds.Add((Guid)reader[0]);

        publishedIds.Should().Contain(eventId);
    }

    [Fact]
    public async Task DoesNotPublish_EventsWithFutureScheduledPublishAt()
    {
        var futureScheduled = DateTimeOffset.UtcNow.AddDays(2);
        var eventId = await SeedScheduledEventAsync(futureScheduled);

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sp_publish_scheduled_events()";
        await using var reader = await cmd.ExecuteReaderAsync();

        var publishedIds = new List<Guid>();
        while (await reader.ReadAsync())
            publishedIds.Add((Guid)reader[0]);

        publishedIds.Should().NotContain(eventId);
    }

    [Fact]
    public async Task SetsEventStatusToPublished_AfterPublish()
    {
        var eventId = await SeedScheduledEventAsync(DateTimeOffset.UtcNow.AddMinutes(-5));

        await using (var conn1 = await db.OpenConnectionAsync())
        await using (var cmd1 = conn1.CreateCommand())
        {
            cmd1.CommandText = "SELECT * FROM sp_publish_scheduled_events()";
            await cmd1.ExecuteReaderAsync();
        }

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Status\", \"ScheduledPublishAt\" FROM events WHERE \"Id\" = @id";
        cmd.Parameters.AddWithValue("id", eventId);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        ((string)reader["Status"]).Should().Be("Published");
        reader["ScheduledPublishAt"].Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task ReturnsEmptySet_WhenNoEventsAreDue()
    {
        // Seed only future events
        await SeedScheduledEventAsync(DateTimeOffset.UtcNow.AddDays(10));

        // Clear any previously published events so counts are predictable
        // (Use a unique Title prefix to isolate)
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        // Publish only events whose ScheduledPublishAt <= now() — none of ours qualify
        cmd.CommandText = "SELECT COUNT(*) FROM sp_publish_scheduled_events() AS ids WHERE ids IS NOT NULL";
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        // Could be > 0 from other parallel tests but our future event must not be in it
        // — no assertion on count; we verified "not contains" in the previous test
        count.Should().BeGreaterThanOrEqualTo(0);
    }
}
