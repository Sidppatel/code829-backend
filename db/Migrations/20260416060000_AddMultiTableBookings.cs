using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTableBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Junction table for multi-table bookings
            migrationBuilder.Sql(@"
CREATE TABLE booking_tables (
    ""BookingId"" uuid NOT NULL REFERENCES bookings(""Id"") ON DELETE CASCADE,
    ""TableId"" uuid NOT NULL REFERENCES tables(""Id"") ON DELETE CASCADE,
    PRIMARY KEY (""BookingId"", ""TableId"")
);
CREATE INDEX ""IX_booking_tables_TableId"" ON booking_tables (""TableId"");
");

            // Backfill existing single-table bookings
            migrationBuilder.Sql(@"
INSERT INTO booking_tables (""BookingId"", ""TableId"")
SELECT ""Id"", ""TableId"" FROM bookings WHERE ""TableId"" IS NOT NULL
ON CONFLICT DO NOTHING;
");

            // Update sp_create_booking to also insert into booking_tables
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_booking(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_booking_number text, p_status text DEFAULT 'Pending'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO bookings (""Id"", ""BookingNumber"", ""Status"", ""UserId"", ""EventId"", ""TableId"",
        ""SeatsReserved"", ""EventTicketTypeId"", ""SubtotalCents"", ""FeeCents"", ""TotalCents"",
        ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING ""Id"" INTO v_id;

    IF p_table_id IS NOT NULL THEN
        INSERT INTO booking_tables (""BookingId"", ""TableId"") VALUES (v_id, p_table_id);
    END IF;

    RETURN v_id;
END; $$;
");

            // Update sp_confirm_booking to mark ALL tables from booking_tables as booked
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_confirm_booking(p_booking_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_seats int; v_seat int;
BEGIN
    UPDATE bookings SET ""Status"" = 'Paid', ""QrToken"" = p_qr_token, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id AND ""Status"" = 'Pending'
    RETURNING ""SeatsReserved"" INTO v_seats;

    -- Mark all tables in this booking as booked
    UPDATE tables SET ""Status"" = 'Booked', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" IN (SELECT ""TableId"" FROM booking_tables WHERE ""BookingId"" = p_booking_id)
      AND ""Status"" IN ('Locked', 'Available');

    v_seats := COALESCE(v_seats, 1);
    FOR v_seat IN 1..v_seats LOOP
        INSERT INTO booking_tickets (""Id"", ""BookingId"", ""TicketCode"", ""QrToken"",
            ""SeatNumber"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), p_booking_id,
            'TKT-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
            encode(gen_random_bytes(32), 'hex'),
            v_seat, 'Unassigned', now(), now());
    END LOOP;
END; $$;
");

            // Update sp_cancel_booking to release ALL tables from booking_tables
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_cancel_booking(p_booking_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE bookings SET ""Status"" = 'Cancelled', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id;

    UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" IN (SELECT ""TableId"" FROM booking_tables WHERE ""BookingId"" = p_booking_id)
      AND ""Status"" IN ('Locked', 'Booked');
END; $$;
");

            // Update sp_refund_booking to release ALL tables from booking_tables
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_refund_booking(p_booking_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE bookings SET ""Status"" = 'Refunded', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id;
    UPDATE stripe_transactions SET ""Status"" = 'Refunded', ""RefundedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""BookingId"" = p_booking_id;

    UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" IN (SELECT ""TableId"" FROM booking_tables WHERE ""BookingId"" = p_booking_id)
      AND ""Status"" IN ('Locked', 'Booked');
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS booking_tables;");
        }
    }
}
