using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class SpConsumeMagicLinkTests(DatabaseFixture db)
{
    private async Task<string> SeedMagicLinkAsync(TimeSpan? expiresIn = null)
    {
        var tokenHash = $"hash-{Guid.NewGuid():N}";
        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(15));

        await db.ExecuteSqlAsync("""
            INSERT INTO magic_link_tokens ("Id","Email","TokenHash","IsUsed","ExpiresAt","CreatedAt","UpdatedAt")
            VALUES (gen_random_uuid(), 'magic@test.com', @hash, false, @exp, now(), now())
            """,
            ("hash", tokenHash), ("exp", expiresAt.UtcDateTime));

        return tokenHash;
    }

    [Fact]
    public async Task Consume_ReturnsTokenData_OnFirstCall()
    {
        var tokenHash = await SeedMagicLinkAsync();

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"Email\", \"ExpiresAt\" FROM sp_consume_magic_link(@hash)";
        cmd.Parameters.AddWithValue("hash", tokenHash);

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.HasRows.Should().BeTrue();
        await reader.ReadAsync();

        reader["Email"].Should().Be("magic@test.com");
        ((Guid)reader["Id"]).Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Consume_ReturnsEmpty_OnDoubleConsume_AtomicGuarantee()
    {
        var tokenHash = await SeedMagicLinkAsync();

        await using (var conn1 = await db.OpenConnectionAsync())
        await using (var cmd1 = conn1.CreateCommand())
        {
            cmd1.CommandText = "SELECT * FROM sp_consume_magic_link(@hash)";
            cmd1.Parameters.AddWithValue("hash", tokenHash);
            await using var r = await cmd1.ExecuteReaderAsync();
            r.HasRows.Should().BeTrue("first consume must return data");
        }

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sp_consume_magic_link(@hash)";
        cmd.Parameters.AddWithValue("hash", tokenHash);

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.HasRows.Should().BeFalse("double consume must be rejected atomically");
    }

    [Fact]
    public async Task Consume_ReturnsEmpty_WhenTokenExpired()
    {
        var tokenHash = await SeedMagicLinkAsync(expiresIn: TimeSpan.FromSeconds(-1));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sp_consume_magic_link(@hash)";
        cmd.Parameters.AddWithValue("hash", tokenHash);

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.HasRows.Should().BeFalse("expired tokens must be rejected");
    }

    [Fact]
    public async Task Consume_ReturnsEmpty_ForUnknownHash()
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sp_consume_magic_link(@hash)";
        cmd.Parameters.AddWithValue("hash", "nonexistent-token-hash");

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.HasRows.Should().BeFalse();
    }

    [Fact]
    public async Task Consume_MarksTokenAsUsed_InDatabase()
    {
        var tokenHash = await SeedMagicLinkAsync();

        await db.ExecuteSqlAsync("SELECT * FROM sp_consume_magic_link(@hash)", ("hash", tokenHash));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"IsUsed\" FROM magic_link_tokens WHERE \"TokenHash\" = @hash";
        cmd.Parameters.AddWithValue("hash", tokenHash);

        var isUsed = (bool?)await cmd.ExecuteScalarAsync();
        isUsed.Should().BeTrue();
    }
}
