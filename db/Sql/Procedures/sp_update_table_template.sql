DROP FUNCTION IF EXISTS sp_update_table_template(uuid, text, int, text, text, int, bool);

CREATE OR REPLACE FUNCTION sp_update_table_template(
    p_id uuid, p_name text, p_capacity int, p_shape text,
    p_color text, p_price_cents int, p_is_active bool,
    p_default_row_span int DEFAULT NULL, p_default_col_span int DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE table_templates SET
        "Name" = COALESCE(p_name, "Name"),
        "DefaultCapacity" = COALESCE(p_capacity, "DefaultCapacity"),
        "DefaultShape" = COALESCE(p_shape, "DefaultShape"),
        "DefaultColor" = p_color,
        "DefaultPriceCents" = COALESCE(p_price_cents, "DefaultPriceCents"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "DefaultRowSpan" = COALESCE(p_default_row_span, "DefaultRowSpan"),
        "DefaultColSpan" = COALESCE(p_default_col_span, "DefaultColSpan"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;
