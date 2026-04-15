using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeTaxColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxCalculationId",
                table: "stripe_transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxTransactionId",
                table: "stripe_transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Drop and recreate view — PostgreSQL can't add columns with CREATE OR REPLACE
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_bookings;");

            // Update sp_create_stripe_transaction to accept tax_calculation_id
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_stripe_transaction;");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_stripe_transaction(
    p_booking_id uuid, p_intent_id text, p_amount_cents int,
    p_transfer_amount_cents int DEFAULT NULL, p_tax_calculation_id text DEFAULT NULL,
    p_currency text DEFAULT 'usd'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO stripe_transactions (""Id"", ""BookingId"", ""PaymentIntentId"", ""Status"",
        ""AmountCents"", ""TransferAmountCents"", ""TaxCalculationId"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_id, p_intent_id, 'RequiresConfirmation',
        p_amount_cents, p_transfer_amount_cents, p_tax_calculation_id, p_currency, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            // New SP to set TaxTransactionId after payment succeeds
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_set_stripe_tax_transaction_id(p_intent_id text, p_tax_transaction_id text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE stripe_transactions SET
        ""TaxTransactionId"" = p_tax_transaction_id,
        ""UpdatedAt"" = now()
    WHERE ""PaymentIntentId"" = p_intent_id;
END; $$;
");

            // Recreate v_bookings view with TaxCalculationId and TaxTransactionId
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
    st.""TaxCalculationId"",
    st.""TaxTransactionId"",
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
            migrationBuilder.DropColumn(
                name: "TaxCalculationId",
                table: "stripe_transactions");

            migrationBuilder.DropColumn(
                name: "TaxTransactionId",
                table: "stripe_transactions");
        }
    }
}
