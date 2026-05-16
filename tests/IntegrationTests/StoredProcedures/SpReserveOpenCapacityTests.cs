using IntegrationTests.Fixtures;
using Npgsql;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpReserveOpenCapacityTests(DatabaseFixture db)
{

    private async Task<Guid> SeedEventAsync(int maxCapacity = 50)
    {
        return await TestSeed.SeedEventAsync(db, new TestSeed.EventOptions(MaxCapacity: maxCapacity));
    }

    private async Task<(Guid userId, Guid ticketTypeId)> SeedUserAndTicketTypeAsync(Guid eventId, int quota = 10)
    {
        var userId = await TestSeed.SeedUserAsync(db);
        var ttId = await TestSeed.SeedEventTicketTypeAsync(db, eventId, quota);
        return (userId, ttId);
    }

    [Fact]
    public async Task ReturnsNewPurchaseId_WhenCapacityAvailable()
    {
        var eventId = await SeedEventAsync(50);
        var (userId, ttId) = await SeedUserAndTicketTypeAsync(eventId);

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sp_reserve_open_capacity(@uid, @ev, @seats, @tt, @sub, @fee, @tot, @pnum)";
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("ev", eventId);
        cmd.Parameters.AddWithValue("seats", 2);
        cmd.Parameters.AddWithValue("tt", ttId);
        cmd.Parameters.AddWithValue("sub", 2000);
        cmd.Parameters.AddWithValue("fee", 100);
        cmd.Parameters.AddWithValue("tot", 2100);
        cmd.Parameters.AddWithValue("pnum", $"PUR-{Guid.NewGuid():N}"[..12]);

        var result = await cmd.ExecuteScalarAsync();

        result.Should().NotBeNull();
        result.Should().BeOfType<Guid>().Which.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ThrowsException_WhenEventNotFound()
    {
        var userId = Guid.NewGuid();
        var nonExistentEvent = Guid.NewGuid();
        var ttId = Guid.NewGuid();

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sp_reserve_open_capacity(@uid, @ev, @seats, @tt, @sub, @fee, @tot, @pnum)";
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("ev", nonExistentEvent);
        cmd.Parameters.AddWithValue("seats", 1);
        cmd.Parameters.AddWithValue("tt", ttId);
        cmd.Parameters.AddWithValue("sub", 500);
        cmd.Parameters.AddWithValue("fee", 25);
        cmd.Parameters.AddWithValue("tot", 525);
        cmd.Parameters.AddWithValue("pnum", "PUR-NOTFOUND");

        var act = () => cmd.ExecuteScalarAsync();
        await act.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task ThrowsException_WhenCapacityExceeded()
    {
        var eventId = await SeedEventAsync(1);
        var (userId, ttId) = await SeedUserAndTicketTypeAsync(eventId, quota: 10);

        await TestSeed.SeedPurchaseAsync(db, userId, eventId, status: "Paid");

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sp_reserve_open_capacity(@uid, @ev, @seats, @tt, @sub, @fee, @tot, @pnum)";
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("ev", eventId);
        cmd.Parameters.AddWithValue("seats", 1);
        cmd.Parameters.AddWithValue("tt", ttId);
        cmd.Parameters.AddWithValue("sub", 1000);
        cmd.Parameters.AddWithValue("fee", 50);
        cmd.Parameters.AddWithValue("tot", 1050);
        cmd.Parameters.AddWithValue("pnum", "PUR-OVERFLOW");

        var act = () => cmd.ExecuteScalarAsync();
        await act.Should().ThrowAsync<PostgresException>();
    }
}
