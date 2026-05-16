using IntegrationTests.Fixtures;
using Npgsql;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpCheckInTicketTests(DatabaseFixture db)
{
    private async Task<(Guid purchaseId, string qrToken)> SeedPurchaseWithTicketAsync()
    {
        var userId = await TestSeed.SeedUserAsync(db);
        var eventId = await TestSeed.SeedEventAsync(db);
        var qrToken = $"test-qr-{Guid.NewGuid():N}";

        var purchaseId = await TestSeed.SeedPurchaseAsync(db, userId, eventId);
        await TestSeed.SeedTicketAsync(db, purchaseId, eventId, userId, qrToken);

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

        await using (var conn1 = await db.OpenConnectionAsync())
        await using (var cmd1 = conn1.CreateCommand())
        {
            cmd1.CommandText = "SELECT * FROM sp_check_in_ticket(@qr)";
            cmd1.Parameters.AddWithValue("qr", qrToken);
            await cmd1.ExecuteReaderAsync();
        }

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
