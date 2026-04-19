CREATE OR REPLACE FUNCTION sp_update_table_template(
    p_id uuid, p_name text, p_capacity int, p_shape text,
    p_color text, p_price_cents int, p_is_active bool
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE table_templates SET
        "Name" = COALESCE(p_name, "Name"),
        "DefaultCapacity" = COALESCE(p_capacity, "DefaultCapacity"),
        "DefaultShape" = COALESCE(p_shape, "DefaultShape"),
        "DefaultColor" = p_color,
        "DefaultPriceCents" = COALESCE(p_price_cents, "DefaultPriceCents"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;