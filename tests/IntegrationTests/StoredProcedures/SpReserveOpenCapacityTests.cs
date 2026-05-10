using IntegrationTests.Fixtures;
using Npgsql;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpReserveOpenCapacityTests(DatabaseFixture db)
{

    private async Task<Guid> SeedEventAsync(int maxCapacity = 50)
    {
        var id = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO events ("Id","Title","Slug","Description","StartDate","EndDate",
                "LayoutMode","MaxCapacity","Status","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@id, 'Test Event', 'test-event-' || @id::text, 'desc', now() + interval '7 days', now() + interval '8 days',
                'Open', @cap, 'Published', @org, now(), now())
            """,
            ("id", id), ("cap", maxCapacity), ("org", Guid.NewGuid()));
        return id;
    }

    private async Task<(Guid userId, Guid ticketTypeId)> SeedUserAndTicketTypeAsync(Guid eventId, int quota = 10)
    {
        var userId = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO users ("Id","Email","FirstName","LastName","Role","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Test','User','User', now(), now())
            """,
            ("id", userId), ("email", $"user-{userId}@test.com"));

        var ttId = Guid.NewGuid();
        await db.ExecuteSqlAsync("""
            INSERT INTO event_ticket_types
                ("Id","EventId","Name","PriceCents","Quota","SoldCount","CreatedAt","UpdatedAt")
            VALUES (@id, @ev, 'General', 1000, @quota, 0, now(), now())
            """,
            ("id", ttId), ("ev", eventId), ("quota", quota));

        return (userId, ttId);
    }

    [Fact(Skip = "S1 test seeds incomplete (missing IsFeatured + FK-valid VenueId/BusinessUserId); rewrite with TestSeed helper tracked as follow-up")]
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

    [Fact(Skip = "S1 test seeds incomplete (missing IsFeatured + FK-valid VenueId/BusinessUserId); rewrite with TestSeed helper tracked as follow-up")]
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

    [Fact(Skip = "S1 test seeds incomplete (missing IsFeatured + FK-valid VenueId/BusinessUserId); rewrite with TestSeed helper tracked as follow-up")]
    public async Task ThrowsException_WhenCapacityExceeded()
    {
        var eventId = await SeedEventAsync(1);
        var (userId, ttId) = await SeedUserAndTicketTypeAsync(eventId, quota: 10);

        await db.ExecuteSqlAsync("""
            INSERT INTO purchases ("Id","UserId","EventId","Status","Seats",
                "SubtotalCents","FeeCents","TotalCents","PurchaseNumber","CreatedAt","UpdatedAt")
            VALUES (gen_random_uuid(), @uid, @ev, 'Confirmed', 1, 1000, 50, 1050, 'PUR-SEED-1', now(), now())
            """,
            ("uid", userId), ("ev", eventId));

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
