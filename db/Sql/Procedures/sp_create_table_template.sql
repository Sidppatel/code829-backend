CREATE OR REPLACE FUNCTION sp_create_table_template(
    p_name text, p_capacity int, p_shape text, p_color text, p_price_cents int
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO table_templates ("Id", "Name", "DefaultCapacity", "DefaultShape",
        "DefaultColor", "DefaultPriceCents", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_name, p_capacity, p_shape,
        p_color, p_price_cents, true, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;