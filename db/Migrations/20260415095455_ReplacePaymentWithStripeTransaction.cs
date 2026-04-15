using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePaymentWithStripeTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "stripe_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    TransferAmountCents = table.Column<int>(type: "integer", nullable: true),
                    TotalChargedCents = table.Column<int>(type: "integer", nullable: true),
                    TaxAmountCents = table.Column<int>(type: "integer", nullable: true),
                    StripeFeesCents = table.Column<int>(type: "integer", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stripe_transactions", x => x.Id);
                    table.CheckConstraint("CK_stripe_transactions_AmountCents", "\"AmountCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_Currency", "\"Currency\" IN ('usd')");
                    table.CheckConstraint("CK_stripe_transactions_NotRefundedNoRefundDate", "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");
                    table.CheckConstraint("CK_stripe_transactions_PaidLifecycle", "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");
                    table.CheckConstraint("CK_stripe_transactions_PendingNoPaidDate", "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");
                    table.CheckConstraint("CK_stripe_transactions_RefundLifecycle", "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_stripe_transactions_Status", "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                    table.CheckConstraint("CK_stripe_transactions_StripeFees", "\"StripeFeesCents\" IS NULL OR \"StripeFeesCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TaxAmount", "\"TaxAmountCents\" IS NULL OR \"TaxAmountCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TotalCharged", "\"TotalChargedCents\" IS NULL OR \"TotalChargedCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TransferAmount", "\"TransferAmountCents\" IS NULL OR \"TransferAmountCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_stripe_transactions_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transactions_BookingId",
                table: "stripe_transactions",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transactions_PaymentIntentId",
                table: "stripe_transactions",
                column: "PaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transactions_Status_PaidAt",
                table: "stripe_transactions",
                columns: new[] { "Status", "PaidAt" });

            // Drop old payment stored procedures
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_payment;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_payment_status;");

            // Create new stripe transaction stored procedures
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_stripe_transaction(
    p_booking_id uuid, p_intent_id text, p_amount_cents int,
    p_transfer_amount_cents int DEFAULT NULL, p_currency text DEFAULT 'usd'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO stripe_transactions (""Id"", ""BookingId"", ""PaymentIntentId"", ""Status"",
        ""AmountCents"", ""TransferAmountCents"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_id, p_intent_id, 'RequiresConfirmation',
        p_amount_cents, p_transfer_amount_cents, p_currency, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_stripe_transaction_status(p_intent_id text, p_status text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE stripe_transactions SET
        ""Status"" = p_status,
        ""PaidAt"" = CASE WHEN p_status IN ('Succeeded','Refunded') AND ""PaidAt"" IS NULL THEN now() ELSE ""PaidAt"" END,
        ""RefundedAt"" = CASE WHEN p_status = 'Refunded' THEN now() ELSE ""RefundedAt"" END,
        ""UpdatedAt"" = now()
    WHERE ""PaymentIntentId"" = p_intent_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_enrich_stripe_transaction(
    p_intent_id text, p_total_charged_cents int, p_tax_amount_cents int, p_stripe_fees_cents int
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE stripe_transactions SET
        ""TotalChargedCents"" = p_total_charged_cents,
        ""TaxAmountCents"" = p_tax_amount_cents,
        ""StripeFeesCents"" = p_stripe_fees_cents,
        ""UpdatedAt"" = now()
    WHERE ""PaymentIntentId"" = p_intent_id;
END; $$;
");

            // Recreate v_bookings view to join on stripe_transactions instead of payments
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_bookings AS
SELECT
    b.""Id"", b.""BookingNumber"", b.""Status""::text,
    b.""SubtotalCents"", b.""FeeCents"", b.""TotalCents"",
    b.""QrToken"", b.""SeatsReserved"", b.""CreatedAt"",
    b.""UserId"",
    u.""Email"" AS ""UserEmail"",
    u.""FirstName"" AS ""UserFirstName"",
    u.""LastName"" AS ""UserLastName"",
    b.""EventId"",
    e.""Title"" AS ""EventTitle"",
    e.""Slug"" AS ""EventSlug"",
    e.""StartDate"" AS ""EventStartDate"",
    e.""EndDate"" AS ""EventEndDate"",
    COALESCE(e.""Category""::text, '') AS ""EventCategory"",
    e.""ImagePath"" AS ""EventImagePath"",
    v.""Name"" AS ""VenueName"",
    COALESCE(addr.""Line1"", '') AS ""VenueAddress"",
    COALESCE(addr.""City"", '') AS ""VenueCity"",
    COALESCE(addr.""State"", '') AS ""VenueState"",
    b.""TableId"",
    tbl.""Label"" AS ""TableLabel"",
    b.""EventTicketTypeId"",
    ett.""Label"" AS ""EventTicketTypeLabel"",
    st.""Id"" AS ""StripeTransactionId"",
    st.""PaymentIntentId"",
    st.""Status""::text AS ""PaymentStatus"",
    st.""AmountCents"" AS ""PaymentAmountCents"",
    st.""TotalChargedCents"",
    st.""TaxAmountCents"",
    st.""StripeFeesCents"",
    st.""TransferAmountCents"",
    st.""PaidAt"", st.""RefundedAt"",
    COALESCE(tc.cnt, 0)::int AS ""TicketCount"",
    e.""OrganizerId""
FROM bookings b
JOIN users u ON b.""UserId"" = u.""Id""
JOIN events e ON b.""EventId"" = e.""Id""
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses addr ON v.""AddressId"" = addr.""Id""
LEFT JOIN tables tbl ON b.""TableId"" = tbl.""Id""
LEFT JOIN event_ticket_types ett ON b.""EventTicketTypeId"" = ett.""Id""
LEFT JOIN stripe_transactions st ON st.""BookingId"" = b.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM booking_tickets bt WHERE bt.""BookingId"" = b.""Id""
) tc ON true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stripe_transactions");

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentIntentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RefundId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.CheckConstraint("CK_payments_AmountCents", "\"AmountCents\" >= 0");
                    table.CheckConstraint("CK_payments_Currency", "\"Currency\" IN ('usd')");
                    table.CheckConstraint("CK_payments_NotRefundedNoRefundDate", "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");
                    table.CheckConstraint("CK_payments_PaidLifecycle", "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");
                    table.CheckConstraint("CK_payments_PendingNoPaidDate", "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");
                    table.CheckConstraint("CK_payments_RefundLifecycle", "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_payments_Status", "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                    table.ForeignKey(
                        name: "FK_payments_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_BookingId",
                table: "payments",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_PaymentIntentId",
                table: "payments",
                column: "PaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_Status_PaidAt",
                table: "payments",
                columns: new[] { "Status", "PaidAt" });
        }
    }
}
