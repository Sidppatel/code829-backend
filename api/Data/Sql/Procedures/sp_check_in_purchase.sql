CREATE OR REPLACE FUNCTION sp_check_in_purchase(p_qr_token text)
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
    v_purchase_id uuid;
    v_purchase_number text;
    v_purchase_status text;
    v_updated_at timestamptz;
    v_event_title text;
    v_user_name text;
BEGIN
    SELECT p."Id", p."PurchaseNumber", p."Status", p."UpdatedAt",
           e."Title", u."FirstName" || ' ' || u."LastName"
      INTO v_purchase_id, v_purchase_number, v_purchase_status, v_updated_at,
           v_event_title, v_user_name
    FROM purchases p
    JOIN events e ON e."Id" = p."EventId"
    JOIN users u ON u."Id" = p."UserId"
    WHERE p."QrToken" = p_qr_token
    FOR UPDATE OF p;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    IF v_purchase_status = 'CheckedIn' THEN
        RETURN QUERY SELECT
            false, 'Already checked in'::text,
            v_purchase_number, v_user_name, v_event_title,
            'CheckedIn'::text, v_updated_at;
        RETURN;
    END IF;

    IF v_purchase_status <> 'Paid' THEN
        RETURN QUERY SELECT
            false,
            ('Purchase is ' || v_purchase_status || ' — cannot check in')::text,
            v_purchase_number, v_user_name, v_event_title,
            v_purchase_status::text, NULL::timestamptz;
        RETURN;
    END IF;

    UPDATE purchases
       SET "Status" = 'CheckedIn', "UpdatedAt" = now()
     WHERE "Id" = v_purchase_id;

    RETURN QUERY SELECT
        true, 'Check-in successful'::text,
        v_purchase_number, v_user_name, v_event_title,
        'CheckedIn'::text, now();
END;
$$;
