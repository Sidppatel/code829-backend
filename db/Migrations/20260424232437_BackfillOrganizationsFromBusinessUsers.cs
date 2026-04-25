using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Pure data migration. Creates a 1:1 Organization for every existing
    /// BusinessUser that already has a Stripe connected account or is an Admin,
    /// then links business_users.OrganizationId to the new row. The SQL body
    /// lives in db/Sql/Migrations/BackfillOrganizationsFromBusinessUsers.sql so
    /// the data migration can be re-read for code review without scrolling
    /// through generated EF migration code.
    /// </remarks>
    public partial class BackfillOrganizationsFromBusinessUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("BackfillOrganizationsFromBusinessUsers.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down is intentionally minimal: dev DBs are rebuilt via
            // stop-clear-start (per project policy in CLAUDE.md), and prod
            // rollbacks restore from Supabase backups. Reversing a backfill
            // safely (without losing the manually-attached members the
            // developer may have added later) requires more context than a
            // migration can carry, so this is left as a no-op.
        }
    }
}
