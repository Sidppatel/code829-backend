CREATE OR REPLACE FUNCTION sp_create_event_ticket_type(
    p_event_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int,
    p_description text DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_ticket_types ("Id", "EventId", "Label", "PriceCents", "PlatformFeeCents",
        "MaxQuantity", "SortOrder", "Description", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_event_id, p_label, p_price_cents, p_platform_fee_cents,
        p_max_quantity, p_sort_order, p_description, true, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;