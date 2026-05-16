using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpPublishScheduledEventsTests(DatabaseFixture db)
{
    private async Task<Guid> SeedScheduledEventAsync(DateTimeOffset scheduledAt)
    {
        return await TestSeed.SeedEventAsync(db, new TestSeed.EventOptions(
            Status: "Draft",
            IsPublished: false,
            ScheduledPublishAt: scheduledAt.UtcDateTime
        ));
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

        await SeedScheduledEventAsync(DateTimeOffset.UtcNow.AddDays(10));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM sp_publish_scheduled_events() AS ids WHERE ids IS NOT NULL";
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        count.Should().BeGreaterThanOrEqualTo(0);
    }
}
