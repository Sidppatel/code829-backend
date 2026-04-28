using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Installs every stored procedure and view from the embedded
    /// <c>db/Sql/**</c> tree. The squashed InitialCreate migration only
    /// re-creates the schema; without this migration the API would fail at
    /// runtime on the first SP call. Idempotent — every artifact is defined
    /// with <c>CREATE OR REPLACE</c>, so reapplying is safe.
    /// </remarks>
    public partial class InstallSqlArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.ViewsOrg");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Procedures");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.ProceduresOrg");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.ProceduresStripe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: we don't track every artifact name and dropping all SPs/views
            // generically is risky. A future schema squash can replace this body.
        }
    }
}
