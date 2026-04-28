CREATE OR REPLACE FUNCTION sp_reserve_open_capacity(
    p_user_id uuid,
    p_event_id uuid,
    p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int,
    p_fee_cents int,
    p_total_cents int,
    p_purchase_number text
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_layout text;
    v_max_capacity int;
    v_total_reserved int;
    v_tt_max int;
    v_tt_sold int;
BEGIN
    SELECT "LayoutMode", "MaxCapacity"
      INTO v_layout, v_max_capacity
      FROM events
      WHERE "Id" = p_event_id
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

    SELECT COALESCE(SUM("SeatsReserved"), 0)
      INTO v_total_reserved
      FROM purchases
      WHERE "EventId" = p_event_id
        AND "Status" IN ('Pending', 'Paid', 'CheckedIn')
        AND "SeatsReserved" IS NOT NULL;

    IF v_total_reserved + p_seats > v_max_capacity THEN
        RAISE EXCEPTION 'Not enough capacity. Available: %, requested: %',
            v_max_capacity - v_total_reserved, p_seats USING ERRCODE = '23514';
    END IF;

    IF p_event_ticket_type_id IS NOT NULL THEN
        SELECT "MaxQuantity" INTO v_tt_max
          FROM event_ticket_types
          WHERE "Id" = p_event_ticket_type_id
          FOR UPDATE;

        IF v_tt_max IS NOT NULL THEN
            SELECT COALESCE(SUM("SeatsReserved"), 0)
              INTO v_tt_sold
              FROM purchases
              WHERE "EventTicketTypeId" = p_event_ticket_type_id
                AND "Status" IN ('Pending', 'Paid', 'CheckedIn')
                AND "SeatsReserved" IS NOT NULL;

            IF v_tt_sold + p_seats > v_tt_max THEN
                RAISE EXCEPTION 'Not enough availability for ticket type. Available: %, requested: %',
                    v_tt_max - v_tt_sold, p_seats USING ERRCODE = '23514';
            END IF;
        END IF;
    END IF;

    INSERT INTO purchases ("Id", "PurchaseNumber", "Status", "UserId", "EventId", "TableId",
        "SeatsReserved", "EventTicketTypeId", "SubtotalCents", "FeeCents", "TotalCents",
        "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_purchase_number, 'Pending', p_user_id, p_event_id, NULL,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING "Id" INTO v_id;

    RETURN v_id;
END; $$;