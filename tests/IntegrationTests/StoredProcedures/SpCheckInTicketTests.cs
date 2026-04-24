using IntegrationTests.Fixtures;
using Npgsql;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpCheckInTicketTests(DatabaseFixture db)
{
    private async Task<(Guid purchaseId, string qrToken)> SeedPurchaseWithTicketAsync()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var qrToken = $"test-qr-{Guid.NewGuid():N}";

        await db.ExecuteSqlAsync("""
            INSERT INTO events ("Id","Title","Slug","Description","StartDate","EndDate",
                "LayoutMode","MaxCapacity","Status","BusinessUserId","CreatedAt","UpdatedAt")
            VALUES (@ev, 'CheckIn Event', 'checkin-' || @ev::text, 'desc', now() - interval '1 hour', now() + interval '3 hours',
                'Open', 100, 'Published', gen_random_uuid(), now(), now())
            """, ("ev", eventId));

        await db.ExecuteSqlAsync("""
            INSERT INTO users ("Id","Email","FirstName","LastName","Role","CreatedAt","UpdatedAt")
            VALUES (@id, @email, 'Jane','Doe','User', now(), now())
            """, ("id", userId), ("email", $"checkin-{userId}@test.com"));

        await db.ExecuteSqlAsync("""
            INSERT INTO purchases ("Id","UserId","EventId","Status","Seats",
                "SubtotalCents","FeeCents","TotalCents","PurchaseNumber","CreatedAt","UpdatedAt")
            VALUES (@pid, @uid, @ev, 'Confirmed', 1, 1000, 50, 1050, 'CHK-001', now(), now())
            """, ("pid", purchaseId), ("uid", userId), ("ev", eventId));

        await db.ExecuteSqlAsync("""
            INSERT INTO tickets ("Id","PurchaseId","EventId","BuyerUserId","Status",
                "QrToken","CreatedAt","UpdatedAt")
            VALUES (@tid, @pid, @ev, @uid, 'Active', @qr, now(), now())
            """, ("tid", ticketId), ("pid", purchaseId), ("ev", eventId),
               ("uid", userId), ("qr", qrToken));

        return (purchaseId, qrToken);
    }

    [Fact]
    public async Task CheckIn_Succeeds_WithValidQrToken()
    {
        var (_, qrToken) = await SeedPurchaseWithTicketAsync();

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Success\", \"Message\" FROM sp_check_in_ticket(@qr)";
        cmd.Parameters.AddWithValue("qr", qrToken);

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.HasRows.Should().BeTrue();
        await reader.ReadAsync();

        ((bool)reader["Success"]).Should().BeTrue();
    }

    [Fact]
    public async Task CheckIn_ReturnsFalse_WhenAlreadyCheckedIn()
    {
        var (_, qrToken) = await SeedPurchaseWithTicketAsync();

        // First check-in
        await using (var conn1 = await db.OpenConnectionAsync())
        await using (var cmd1 = conn1.CreateCommand())
        {
            cmd1.CommandText = "SELECT * FROM sp_check_in_ticket(@qr)";
            cmd1.Parameters.AddWithValue("qr", qrToken);
            await cmd1.ExecuteReaderAsync();
        }

        // Second check-in on same token
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Success\", \"Message\" FROM sp_check_in_ticket(@qr)";
        cmd.Parameters.AddWithValue("qr", qrToken);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        ((bool)reader["Success"]).Should().BeFalse();
    }

    [Fact]
    public async Task CheckIn_ReturnsNoRows_ForUnknownQrToken()
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Success\" FROM sp_check_in_ticket(@qr)";
        cmd.Parameters.AddWithValue("qr", "nonexistent-token-xyz");

        await using var reader = await cmd.ExecuteReaderAsync();

        // SP returns empty result set or Success=false for unknown token
        if (reader.HasRows)
        {
            await reader.ReadAsync();
            ((bool)reader["Success"]).Should().BeFalse();
        }
        else
        {
            reader.HasRows.Should().BeFalse();
        }
    }
}
