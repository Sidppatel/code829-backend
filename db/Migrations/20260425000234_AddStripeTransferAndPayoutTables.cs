using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds the stripe_transfers + stripe_payouts audit tables fed by the
    /// Connect webhook handlers (transfer.created, payout.created, payout.paid).
    /// SP bodies live in db/Sql/ProceduresStripe/ so the table-create migration
    /// pulls them in via MigrationSqlLoader.LoadAll. Same load-order pattern as
    /// AddOrganizationsTable: the SPs reference these tables and would fail to
    /// CREATE FUNCTION before the tables exist.
    /// </remarks>
    public partial class AddStripeTransferAndPayoutTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stripe_payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    StripePayoutId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "usd"),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ArrivalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawEvent = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stripe_payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stripe_payouts_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stripe_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    StripeTransferId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "usd"),
                    RawEvent = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stripe_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stripe_transfers_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stripe_transfers_purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stripe_payouts_OrganizationId",
                table: "stripe_payouts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_stripe_payouts_StripePayoutId",
                table: "stripe_payouts",
                column: "StripePayoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transfers_OrganizationId",
                table: "stripe_transfers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transfers_PurchaseId",
                table: "stripe_transfers",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transfers_StripeTransferId",
                table: "stripe_transfers",
                column: "StripeTransferId",
                unique: true);

            // Load the Stripe-event SPs (sp_insert_stripe_transfer +
            // sp_update_stripe_payout) now that the tables they reference
            // exist. Same idempotent CREATE OR REPLACE pattern as the org
            // procedures — re-running the migration is safe.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.ProceduresStripe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_insert_stripe_transfer(text, text, text, int, text, jsonb);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_stripe_payout(text, text, int, text, text, timestamptz, timestamptz, jsonb);");

            migrationBuilder.DropTable(
                name: "stripe_payouts");

            migrationBuilder.DropTable(
                name: "stripe_transfers");
        }
    }
}
