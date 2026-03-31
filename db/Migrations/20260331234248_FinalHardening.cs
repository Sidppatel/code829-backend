using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class FinalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_NotRefundedNoRefundDate",
                table: "payments",
                sql: "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_PendingNoPaidDate",
                table: "payments",
                sql: "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_CompletedRequiresPublish",
                table: "events",
                sql: "\"Status\" <> 'Completed' OR \"PublishedAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_DraftNoPublishDate",
                table: "events",
                sql: "\"Status\" <> 'Draft' OR \"PublishedAt\" IS NULL");

            // ─── Restrict view privileges to SELECT only ─────────────
            // ALTER DEFAULT PRIVILEGES grants full DML on all tables including views.
            // Views are read-only projections — revoke write access for app and readonly roles.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_app') THEN
        EXECUTE 'REVOKE INSERT, UPDATE, DELETE ON v_events, v_ticket_types, v_tables, v_pricing_rules, v_event_summary FROM ep_app';
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_readonly') THEN
        EXECUTE 'REVOKE INSERT, UPDATE, DELETE ON v_events, v_ticket_types, v_tables, v_pricing_rules, v_event_summary FROM ep_readonly';
    END IF;
END
$$;
");

            // ─── Updated table comments ──────────────────────────────
            migrationBuilder.Sql("COMMENT ON TABLE payments IS 'Stripe payment records linked 1:1 to bookings. One payment intent per booking by design — Stripe handles retries internally within the same intent. If multiple payment attempts or partial refunds are needed, redesign as payment-history table.';");
            migrationBuilder.Sql("COMMENT ON COLUMN bookings.\"QrToken\" IS 'Raw bearer token embedded in QR code images for check-in scanning. Stored unhashed because the token must be displayable to users and scannable by staff. 192-bit random value. Check-in requires staff auth as defense-in-depth.';");
            migrationBuilder.Sql("COMMENT ON COLUMN booking_items.\"QrToken\" IS 'Per-seat/ticket QR token for individual check-in. Stored unhashed — see bookings.QrToken comment for rationale.';");
            migrationBuilder.Sql("COMMENT ON COLUMN booking_items.\"InvitationToken\" IS 'Shareable invitation link token for guest access (no login required). Stored unhashed because guests use the raw token in URL paths for anonymous access. 256-bit random value.';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_NotRefundedNoRefundDate",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_PendingNoPaidDate",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_CompletedRequiresPublish",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_DraftNoPublishDate",
                table: "events");
        }
    }
}
