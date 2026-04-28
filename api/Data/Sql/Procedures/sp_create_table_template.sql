CREATE OR REPLACE FUNCTION sp_create_table_template(
    p_name text, p_capacity int, p_shape text, p_color text, p_price_cents int,
    p_default_row_span int DEFAULT 1, p_default_col_span int DEFAULT 1
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO table_templates ("Id", "Name", "DefaultCapacity", "DefaultShape",
        "DefaultColor", "DefaultPriceCents", "DefaultRowSpan", "DefaultColSpan",
        "IsActive", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_name, p_capacity, p_shape,
        p_color, p_price_cents,
        COALESCE(p_default_row_span, 1), COALESCE(p_default_col_span, 1),
        true, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
