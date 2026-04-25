using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds the paginated/searchable list + count SPs powering the developer
    /// organizations dashboard. The SPs themselves live in
    /// db/Sql/ProceduresOrg/sp_list_organizations.sql and
    /// sp_count_organizations.sql, embedded into the assembly via the
    /// <c>EmbeddedResource Include="Sql/**/*.sql"</c> rule in db.csproj.
    ///
    /// CREATE OR REPLACE FUNCTION makes both SPs idempotent — re-running this
    /// migration after a schema change to the SP body is safe and is the
    /// intended dev workflow per the project's stop-clear-start policy.
    /// </remarks>
    public partial class AddOrganizationListProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_organizations.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_count_organizations.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_organizations(text, boolean, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_count_organizations(text, boolean);");
        }
    }
}
