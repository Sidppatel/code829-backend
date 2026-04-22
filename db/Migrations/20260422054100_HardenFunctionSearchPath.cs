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

            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Procedures");
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
