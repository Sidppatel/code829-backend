using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <summary>
    /// Adds sp_reserve_open_capacity for atomic capacity booking under concurrent load.
    /// Replaces the Redis-lock + SELECT + INSERT pattern which had a race window between
    /// the capacity check and the row insert.
    /// </summary>
    public partial class AddAtomicCapacityReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_reserve_open_capacity(
    p_user_id uuid,
    p_event_id uuid,
    p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int,
    p_fee_cents int,
    p_total_cents int,
    p_booking_number text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE
    v_id uuid;
    v_layout text;
    v_max_capacity int;
    v_total_reserved int;
    v_tt_max int;
    v_tt_sold int;
BEGIN
    -- Serialize concurrent reservations on this event by taking a row-level lock on the event.
    -- Any other worker hitting this sp for the same event will wait here until we commit.
    SELECT ""LayoutMode"", ""MaxCapacity""
      INTO v_layout, v_max_capacity
      FROM events
      WHERE ""Id"" = p_event_id
      FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Event not found' USING ERRCODE = 'P0002';
    END IF;
    IF v_layout <> 'Open' THEN
        RAISE EXCEPTION 'Event is not an Open-capacity event' USING ERRCODE = '22023';
    END IF;
    IF v_max_capacity IS NULL OR v_max_capacity <= 0 THEN
        RAISE EXCEPTION 'Event has no capacity configured' USING ERRCODE = '22023';
    END IF;

    -- Count all active reservations for this event. Under the row lock above, this count
    -- plus our insert is guaranteed to be consistent.
    SELECT COALESCE(SUM(""SeatsReserved""), 0)
      INTO v_total_reserved
      FROM bookings
      WHERE ""EventId"" = p_event_id
        AND ""Status"" IN ('Pending', 'Paid', 'CheckedIn')
        AND ""SeatsReserved"" IS NOT NULL;

    IF v_total_reserved + p_seats > v_max_capacity THEN
        RAISE EXCEPTION 'Not enough capacity. Available: %, requested: %',
            v_max_capacity - v_total_reserved, p_seats USING ERRCODE = '23514';
    END IF;

    -- Ticket-type quota check, same transaction.
    IF p_event_ticket_type_id IS NOT NULL THEN
        SELECT ""MaxQuantity"" INTO v_tt_max
          FROM event_ticket_types
          WHERE ""Id"" = p_event_ticket_type_id
          FOR UPDATE;

        IF v_tt_max IS NOT NULL THEN
            SELECT COALESCE(SUM(""SeatsReserved""), 0)
              INTO v_tt_sold
              FROM bookings
              WHERE ""EventTicketTypeId"" = p_event_ticket_type_id
                AND ""Status"" IN ('Pending', 'Paid', 'CheckedIn')
                AND ""SeatsReserved"" IS NOT NULL;

            IF v_tt_sold + p_seats > v_tt_max THEN
                RAISE EXCEPTION 'Not enough availability for ticket type. Available: %, requested: %',
                    v_tt_max - v_tt_sold, p_seats USING ERRCODE = '23514';
            END IF;
        END IF;
    END IF;

    INSERT INTO bookings (""Id"", ""BookingNumber"", ""Status"", ""UserId"", ""EventId"", ""TableId"",
        ""SeatsReserved"", ""EventTicketTypeId"", ""SubtotalCents"", ""FeeCents"", ""TotalCents"",
        ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_number, 'Pending', p_user_id, p_event_id, NULL,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING ""Id"" INTO v_id;

    RETURN v_id;
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_reserve_open_capacity(uuid, uuid, int, uuid, int, int, int, text);");
        }
    }
}
