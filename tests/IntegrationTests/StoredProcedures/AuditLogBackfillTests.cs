using FluentAssertions;
using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class AuditLogBackfillTests(DatabaseFixture db)
{
    [Fact]
    public async Task SpCreateAuditLog_InsertsRow_WithActorType()
    {
        await db.ExecuteSqlAsync("TRUNCATE audit_logs");

        await using var conn = await db.OpenConnectionAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT sp_create_audit_log(@et, @at, NULL, @st, NULL, @a, NULL, NULL, NULL)";
            cmd.Parameters.AddWithValue("et", "event.created");
            cmd.Parameters.AddWithValue("at", "Admin");
            cmd.Parameters.AddWithValue("st", "Event");
            cmd.Parameters.AddWithValue("a", "event.created");
            var id = (Guid)(await cmd.ExecuteScalarAsync())!;
            id.Should().NotBe(Guid.Empty);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT \"ActorType\", \"EventType\" FROM audit_logs LIMIT 1";
            await using var reader = await cmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader["ActorType"].Should().Be("Admin");
            reader["EventType"].Should().Be("event.created");
        }
    }

    [Fact]
    public async Task SpCreateAuditLog_Rejects_InvalidActorType()
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sp_create_audit_log('x','Invalid',NULL,NULL,NULL,'x',NULL,NULL,NULL)";
        var act = () => cmd.ExecuteScalarAsync();
        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact]
    public async Task VBusinessLogs_Projects_AdminActorRows()
    {
        await db.ExecuteSqlAsync("TRUNCATE audit_logs");

        await db.ExecuteSqlAsync(
            "SELECT sp_create_audit_log(@et,@at,NULL,@st,NULL,@a,@m,NULL,NULL)",
            ("et", "event.created"),
            ("at", "Admin"),
            ("st", "Event"),
            ("a", "event.created"),
            ("m", """{"description":"organizer published event"}"""));

        await db.ExecuteSqlAsync(
            "SELECT sp_create_audit_log(@et,@at,NULL,NULL,NULL,@a,NULL,NULL,NULL)",
            ("et", "Exception"),
            ("at", "System"),
            ("a", "InvalidOperationException"));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*)::int FROM v_business_logs";
        var adminCount = (int)(await cmd.ExecuteScalarAsync())!;
        adminCount.Should().Be(1);

        await using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT \"Description\" FROM v_business_logs LIMIT 1";
        var desc = (string?)await cmd2.ExecuteScalarAsync();
        desc.Should().Be("organizer published event");
    }
}
