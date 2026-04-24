using FluentAssertions;
using IntegrationTests.Fixtures;

namespace IntegrationTests.StoredProcedures;

[Collection("Database")]
public sealed class AuditLogBackfillTests(DatabaseFixture db)
{
    private const string BackfillSql = """
        INSERT INTO audit_logs (
            "Id", "CreatedAt", "EventType", "ActorType", "ActorId",
            "SubjectType", "SubjectId", "Action", "MetadataJson", "Ip", "CorrelationId"
        )
        SELECT gen_random_uuid(), "Timestamp", "Action", 'Admin', "BusinessUserId",
               "EntityType", "EntityId", "Action",
               CASE WHEN "MetadataJson" IS NULL THEN NULL
                    WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
                    ELSE jsonb_build_object('raw', "MetadataJson") END,
               NULLIF("IpAddress", ''), NULL::uuid
        FROM business_logs
        UNION ALL
        SELECT gen_random_uuid(), "Timestamp", COALESCE("ExceptionType", 'developer.log'),
               'Developer', "BusinessUserId", NULL, NULL, "Message",
               CASE WHEN "MetadataJson" IS NULL THEN NULL
                    WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
                    ELSE jsonb_build_object('raw', "MetadataJson") END,
               NULLIF("IpAddress", ''), NULL::uuid
        FROM developer_logs
        UNION ALL
        SELECT gen_random_uuid(), "Timestamp", "Category"::text, 'System', "UserId",
               "EntityType", "EntityId", "Action",
               CASE WHEN "MetadataJson" IS NULL THEN NULL
                    WHEN "MetadataJson" ~ '^\s*[{\[]' THEN "MetadataJson"::jsonb
                    ELSE jsonb_build_object('raw', "MetadataJson") END,
               NULL, NULL::uuid
        FROM system_logs;
        """;

    private async Task TruncateAllAsync()
    {
        await db.ExecuteSqlAsync(
            "TRUNCATE audit_logs, business_logs, developer_logs, system_logs RESTART IDENTITY");
    }

    [Fact]
    public async Task Backfill_MergesAllThreeLegacyTables_IntoAuditLogs()
    {
        await TruncateAllAsync();

        for (var i = 0; i < 5; i++)
        {
            await db.ExecuteSqlAsync("""
                INSERT INTO business_logs ("Id","Timestamp","Action","EntityType")
                VALUES (gen_random_uuid(), now(), @a, 'Event')
                """, ("a", $"admin.action.{i}"));
        }
        for (var i = 0; i < 3; i++)
        {
            await db.ExecuteSqlAsync("""
                INSERT INTO developer_logs ("Id","Timestamp","Severity","Message")
                VALUES (gen_random_uuid(), now(), 'Error', @m)
                """, ("m", $"boom-{i}"));
        }
        for (var i = 0; i < 2; i++)
        {
            await db.ExecuteSqlAsync("""
                INSERT INTO system_logs ("Id","Timestamp","Category","Action")
                VALUES (gen_random_uuid(), now(), 'BackgroundWorker', @a)
                """, ("a", $"worker.{i}"));
        }

        await db.ExecuteSqlAsync(BackfillSql);

        await using var conn = await db.OpenConnectionAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*)::int FROM audit_logs";
            var total = (int)(await cmd.ExecuteScalarAsync())!;
            total.Should().Be(10);
        }

        var counts = new Dictionary<string, int>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT \"ActorType\", COUNT(*)::int FROM audit_logs GROUP BY \"ActorType\"";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                counts[(string)reader[0]] = (int)reader[1];
        }

        counts["Admin"].Should().Be(5);
        counts["Developer"].Should().Be(3);
        counts["System"].Should().Be(2);
    }

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
}
