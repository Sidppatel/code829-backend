using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// BE #62 — drops the legacy business_logs / developer_logs / system_logs tables
    /// after the S7 AddAuditLogsTable migration backfilled audit_logs. Views + SPs that
    /// referenced the legacy tables are dropped here; the SP bootstrapper re-creates
    /// them on next startup from db/Sql/Procedures/*.sql and db/Sql/Views/*.sql, which
    /// now project over audit_logs (v_business_logs, v_system_logs, v_developer_logs).
    /// </remarks>
    public partial class DropLegacyLogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop legacy write SPs (they reference the tables directly).
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_admin_log(text, uuid, text, uuid, text, text, text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_developer_log(text, text, text, text, text, text, int, uuid, text, text, text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_system_log(text, text, text, text, uuid, text, text, uuid, text, int, text);");

            // Drop legacy read SPs (their SETOF types depend on legacy views/tables).
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_admin_logs(text, text, timestamptz, timestamptz, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_count_admin_logs(text, text, timestamptz, timestamptz);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_developer_logs(text, text, timestamptz, timestamptz, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_count_developer_logs(text, text, timestamptz, timestamptz);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_system_logs(timestamptz, text, text, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_cleanup_old_logs(int, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_audit_trigger();");

            // Drop legacy views.
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_business_logs;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_system_logs;");

            // Drop legacy tables.
            migrationBuilder.DropTable(name: "business_logs");
            migrationBuilder.DropTable(name: "developer_logs");
            migrationBuilder.DropTable(name: "system_logs");

            // Final LoadAll for the migration chain. Every table referenced by a view
            // or SP (including audit_logs, which AddAuditLogsTable created in the
            // preceding migration) is now guaranteed to exist. HardenFunctionSearchPath
            // (20260422054100) originally owned this step but ran before audit_logs
            // was introduced, so the loader was moved here.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down is intentionally minimal — re-creates the empty legacy tables only.
            // Project policy (per CLAUDE.md): dev DBs are rebuilt via stop-clear-start
            // rather than rolled back. Prod DB rollbacks restore from Supabase backups.
            migrationBuilder.CreateTable(
                name: "business_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "developer_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developer_logs", x => x.Id);
                    table.CheckConstraint("CK_developer_logs_Severity", "\"Severity\" IN ('Warning','Error','Critical')");
                });

            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_logs", x => x.Id);
                    table.CheckConstraint("CK_system_logs_Category", "\"Category\" IN ('EntityChange','BackgroundWorker','Cache','MockService','Migration')");
                    table.CheckConstraint("CK_system_logs_DurationMs", "\"DurationMs\" IS NULL OR \"DurationMs\" >= 0");
                });

            migrationBuilder.CreateIndex(name: "IX_business_logs_Action", table: "business_logs", column: "Action");
            migrationBuilder.CreateIndex(name: "IX_business_logs_Timestamp", table: "business_logs", column: "Timestamp");
            migrationBuilder.CreateIndex(name: "IX_developer_logs_Severity", table: "developer_logs", column: "Severity");
            migrationBuilder.CreateIndex(name: "IX_developer_logs_Timestamp", table: "developer_logs", column: "Timestamp");
            migrationBuilder.CreateIndex(name: "IX_system_logs_Category", table: "system_logs", column: "Category");
            migrationBuilder.CreateIndex(name: "IX_system_logs_Timestamp", table: "system_logs", column: "Timestamp");
        }
    }
}
