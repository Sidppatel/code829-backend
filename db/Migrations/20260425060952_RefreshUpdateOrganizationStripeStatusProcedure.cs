using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Re-applies <c>sp_update_organization_stripe_status</c> from
    /// <c>db/Sql/ProceduresOrg/sp_update_organization_stripe_status.sql</c>.
    /// The previously-deployed body cast <c>p_requirements_due_json</c> to
    /// text inside COALESCE, which mismatched the jsonb column and threw
    /// <c>42804: COALESCE types text and jsonb cannot be matched</c> on every
    /// account.updated webhook + GetStripeStatus call. The current SQL file
    /// drops the cast; CREATE OR REPLACE FUNCTION makes the swap idempotent.
    ///
    /// Down is a no-op — re-applying the previous broken version isn't useful;
    /// rollback should re-deploy the old binary which carries its own copy.
    /// </remarks>
    public partial class RefreshUpdateOrganizationStripeStatusProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_organization_stripe_status.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
