CREATE OR REPLACE FUNCTION sp_confirm_purchase(p_purchase_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_seats int; v_seat int;
BEGIN
    UPDATE purchases SET "Status" = 'Paid', "QrToken" = p_qr_token, "UpdatedAt" = now()
    WHERE "Id" = p_purchase_id AND "Status" = 'Pending'
    RETURNING "SeatsReserved" INTO v_seats;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    UPDATE tables SET "Status" = 'Booked', "LockedByUserId" = NULL,
        "LockExpiresAt" = NULL, "UpdatedAt" = now()
    WHERE "Id" IN (SELECT "TableId" FROM purchase_tables WHERE "PurchaseId" = p_purchase_id)
      AND "Status" IN ('Locked', 'Available');

    v_seats := COALESCE(v_seats, 1);
    FOR v_seat IN 1..v_seats LOOP
        INSERT INTO purchase_tickets ("Id", "PurchaseId", "TicketCode", "QrToken",
            "SeatNumber", "Status", "CreatedAt", "UpdatedAt")
        VALUES (gen_random_uuid(), p_purchase_id,
            'TKT-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
            encode(gen_random_bytes(32), 'hex'),
            v_seat, 'Unassigned', now(), now());
    END LOOP;
END; $$;