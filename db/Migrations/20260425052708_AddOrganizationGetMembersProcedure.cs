using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds the <c>sp_get_organization_members</c> SP that powers the developer
    /// detail / add-member / remove-member endpoints. The SP body lives at
    /// db/Sql/ProceduresOrg/sp_get_organization_members.sql, embedded into the
    /// assembly via the <c>EmbeddedResource Include="Sql/**/*.sql"</c> rule in
    /// db.csproj.
    ///
    /// CREATE OR REPLACE FUNCTION makes the SP idempotent — re-running this
    /// migration after a body tweak is the intended dev workflow per the
    /// project's stop-clear-start policy.
    /// </remarks>
    public partial class AddOrganizationGetMembersProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_organization_members.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_organization_members(uuid);");
        }
    }
}
