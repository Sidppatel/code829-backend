using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class LifecycleConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_venue_layouts_OneDefaultPerVenue",
                table: "venue_layouts",
                column: "VenueId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.AddCheckConstraint(
                name: "CK_seat_holds_ExpiresAfterCreate",
                table: "seat_holds",
                sql: "\"ExpiresAt\" > \"CreatedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_PaidLifecycle",
                table: "payments",
                sql: "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_RefundLifecycle",
                table: "payments",
                sql: "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_magic_link_tokens_Usage",
                table: "magic_link_tokens",
                sql: "(\"IsUsed\" = false AND \"UsedAt\" IS NULL) OR (\"IsUsed\" = true AND \"UsedAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_PublishLifecycle",
                table: "events",
                sql: "\"Status\" <> 'Published' OR \"PublishedAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_venue_layouts_OneDefaultPerVenue",
                table: "venue_layouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_seat_holds_ExpiresAfterCreate",
                table: "seat_holds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_PaidLifecycle",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_RefundLifecycle",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_magic_link_tokens_Usage",
                table: "magic_link_tokens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_PublishLifecycle",
                table: "events");
        }
    }
}
