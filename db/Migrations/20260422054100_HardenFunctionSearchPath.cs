using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class HardenFunctionSearchPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE SCHEMA IF NOT EXISTS extensions;
                GRANT USAGE ON SCHEMA extensions TO PUBLIC;

                DO $$
                DECLARE current_schema_name text;
                BEGIN
                    SELECT n.nspname INTO current_schema_name
                      FROM pg_extension e
                      JOIN pg_namespace n ON n.oid = e.extnamespace
                     WHERE e.extname = 'pg_trgm';

                    IF current_schema_name IS NOT NULL AND current_schema_name <> 'extensions' THEN
                        EXECUTE 'ALTER EXTENSION pg_trgm SET SCHEMA extensions';
                    ELSIF current_schema_name IS NULL THEN
                        EXECUTE 'CREATE EXTENSION pg_trgm SCHEMA extensions';
                    END IF;
                END $$;
            ");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:extensions.pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            // NOTE: LoadAll("Sql.Views" + "Sql.Procedures") was originally placed here as the
            // "final" loader for the migration chain. It was moved to DropLegacyLogTables
            // (20260424035219) because this migration runs BEFORE AddAuditLogsTable, and
            // the views/SPs embedded at build time now reference audit_logs — which only
            // exists after AddAuditLogsTable runs. DropLegacyLogTables is the first point
            // in the chain where every table referenced by a view or SP is guaranteed
            // to exist.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.pg_trgm", ",,");
        }
    }
}
