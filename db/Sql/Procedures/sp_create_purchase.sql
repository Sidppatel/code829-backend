CREATE OR REPLACE FUNCTION sp_create_purchase(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_purchase_number text, p_status text DEFAULT 'Pending'
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO purchases ("Id", "PurchaseNumber", "Status", "UserId", "EventId", "TableId",
        "SeatsReserved", "EventTicketTypeId", "SubtotalCents", "FeeCents", "TotalCents",
        "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_purchase_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING "Id" INTO v_id;

    IF p_table_id IS NOT NULL THEN
        INSERT INTO purchase_tables ("PurchaseId", "TableId") VALUES (v_id, p_table_id);
    END IF;

    RETURN v_id;
END; $$;