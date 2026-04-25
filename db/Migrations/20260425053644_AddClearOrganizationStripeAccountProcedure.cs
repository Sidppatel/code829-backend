using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds <c>sp_clear_organization_stripe_account</c> — body lives in
    /// <c>db/Sql/ProceduresOrg/sp_clear_organization_stripe_account.sql</c>,
    /// embedded into the assembly via the
    /// <c>EmbeddedResource Include="Sql/**/*.sql"</c> rule in db.csproj.
    ///
    /// CREATE OR REPLACE FUNCTION makes the SP idempotent — re-running this
    /// migration after a body tweak is safe.
    /// </remarks>
    public partial class AddClearOrganizationStripeAccountProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_clear_organization_stripe_account.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_clear_organization_stripe_account(uuid);");
        }
    }
}
