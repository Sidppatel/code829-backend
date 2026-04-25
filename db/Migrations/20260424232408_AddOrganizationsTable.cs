using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "business_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "US"),
                    StripeConnectedAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StripeChargesEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StripePayoutsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StripeDetailsSubmitted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StripeOnboardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StripeRequirementsDue = table.Column<string>(type: "jsonb", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_users_OrganizationId",
                table: "business_users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_ArchivedAt",
                table: "organizations",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_StripeConnectedAccountId",
                table: "organizations",
                column: "StripeConnectedAccountId",
                unique: true,
                filter: "\"StripeConnectedAccountId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_business_users_organizations_OrganizationId",
                table: "business_users",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Load the organization-scoped procedures from the dedicated
            // Sql.ProceduresOrg folder. Kept separate from Sql.Procedures so
            // the LoadAll("Sql.Procedures") at the end of DropLegacyLogTables
            // (which runs BEFORE this migration in the chain) doesn't try to
            // CREATE FUNCTION bodies that reference the organizations table
            // before the table exists. CREATE OR REPLACE FUNCTION is
            // idempotent, so subsequent loads remain safe.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.ProceduresOrg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_business_users_organizations_OrganizationId",
                table: "business_users");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_business_users_OrganizationId",
                table: "business_users");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "business_users");
        }
    }
}
