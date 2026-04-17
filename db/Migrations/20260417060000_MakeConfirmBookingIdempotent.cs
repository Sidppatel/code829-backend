using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <summary>
    /// Makes sp_confirm_booking idempotent. The webhook (payment_intent.succeeded) and the
    /// sync POST /bookings/{id}/confirm can race, each calling this SP. Previously the UPDATE
    /// would no-op on the second call (because Status='Pending' check no longer matched), but
    /// the function still executed the ticket-insert loop, producing a duplicate-key error on
    /// IX_booking_tickets_BookingId_SeatNumber. The guard uses plpgsql's FOUND to short-circuit
    /// when the booking was already confirmed.
    /// </summary>
    public partial class MakeConfirmBookingIdempotent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_confirm_booking(p_booking_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_seats int; v_seat int;
BEGIN
    UPDATE bookings SET ""Status"" = 'Paid', ""QrToken"" = p_qr_token, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id AND ""Status"" = 'Pending'
    RETURNING ""SeatsReserved"" INTO v_seats;

    -- If the UPDATE didn't match (booking already confirmed by concurrent webhook or sync
    -- confirm), skip ticket creation to avoid duplicate-key violations on booking_tickets.
    IF NOT FOUND THEN
        RETURN;
    END IF;

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the pre-idempotent body from 20260416060000_AddMultiTableBookings.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_confirm_booking(p_booking_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_seats int; v_seat int;
BEGIN
    UPDATE bookings SET ""Status"" = 'Paid', ""QrToken"" = p_qr_token, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id AND ""Status"" = 'Pending'
    RETURNING ""SeatsReserved"" INTO v_seats;

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
        }
    }
}
