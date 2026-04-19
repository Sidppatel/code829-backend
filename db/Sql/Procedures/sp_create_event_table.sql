CREATE OR REPLACE FUNCTION sp_create_event_table(
    p_event_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_platform_fee_cents int, p_template_id uuid
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_tables ("Id", "EventId", "Label", "Capacity", "Shape", "Color",
        "PriceCents", "PlatformFeeCents", "IsActive", "TableTemplateId", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_event_id, p_label, p_capacity, p_shape, p_color,
        p_price_cents, p_platform_fee_cents, true, p_template_id, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;