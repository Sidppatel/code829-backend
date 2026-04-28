CREATE OR REPLACE FUNCTION sp_check_in_ticket(p_qr_token text)
RETURNS TABLE(
    "Success" boolean,
    "Message" text,
    "PurchaseNumber" text,
    "GuestName" text,
    "EventTitle" text,
    "StatusStr" text,
    "CheckedInAt" timestamptz
) LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_ticket_id uuid;
    v_purchase_id uuid;
    v_ticket_status text;
    v_seat_number int;
    v_ticket_updated_at timestamptz;
    v_guest_user_id uuid;
    v_buyer_user_id uuid;
    v_purchase_number text;
    v_purchase_status text;
    v_event_title text;
    v_guest_name text;
    v_all_checked boolean;
BEGIN
    SELECT t."Id", t."PurchaseId", t."Status", t."SeatNumber", t."GuestUserId", t."UpdatedAt"
      INTO v_ticket_id, v_purchase_id, v_ticket_status, v_seat_number, v_guest_user_id, v_ticket_updated_at
    FROM purchase_tickets t
    WHERE t."QrToken" = p_qr_token
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    SELECT p."PurchaseNumber", p."Status", p."UserId", e."Title"
      INTO v_purchase_number, v_purchase_status, v_buyer_user_id, v_event_title
    FROM purchases p
    JOIN events e ON e."Id" = p."EventId"
    WHERE p."Id" = v_purchase_id
    FOR UPDATE OF p;

    IF v_guest_user_id IS NOT NULL THEN
        SELECT u."FirstName" || ' ' || u."LastName" INTO v_guest_name
        FROM users u WHERE u."Id" = v_guest_user_id;
    ELSE
        SELECT u."FirstName" || ' ' || u."LastName" INTO v_guest_name
        FROM users u WHERE u."Id" = v_buyer_user_id;
    END IF;

    IF v_ticket_status = 'CheckedIn' THEN
        RETURN QUERY SELECT
            false,
            ('Ticket already checked in (Seat #' || v_seat_number || ')')::text,
            v_purchase_number, v_guest_name, v_event_title,
            'CheckedIn'::text, v_ticket_updated_at;
        RETURN;
    END IF;

    IF v_purchase_status NOT IN ('Paid', 'CheckedIn') THEN
        RETURN QUERY SELECT
            false,
            ('Purchase is ' || v_purchase_status || ' — cannot check in')::text,
            v_purchase_number, v_guest_name, v_event_title,
            v_purchase_status::text, NULL::timestamptz;
        RETURN;
    END IF;

    IF v_ticket_status <> 'Claimed' THEN
        RETURN QUERY SELECT
            false,
            CASE WHEN v_ticket_status = 'Invited'
                THEN 'Ticket invite not yet accepted — recipient must claim it first'
                ELSE 'Ticket has not been claimed yet — assign it to an attendee first'
            END::text,
            v_purchase_number, v_guest_name, v_event_title,
            v_ticket_status::text, NULL::timestamptz;
        RETURN;
    END IF;

    UPDATE purchase_tickets
       SET "Status" = 'CheckedIn', "UpdatedAt" = now()
     WHERE "Id" = v_ticket_id;

    SELECT NOT EXISTS (
        SELECT 1 FROM purchase_tickets
         WHERE "PurchaseId" = v_purchase_id AND "Status" <> 'CheckedIn'
    ) INTO v_all_checked;

    IF v_all_checked THEN
        UPDATE purchases
           SET "Status" = 'CheckedIn', "UpdatedAt" = now()
         WHERE "Id" = v_purchase_id;
    END IF;

    RETURN QUERY SELECT
        true,
        ('Check-in successful — Seat #' || v_seat_number)::text,
        v_purchase_number, v_guest_name, v_event_title,
        'CheckedIn'::text, now();
END;
$$;
