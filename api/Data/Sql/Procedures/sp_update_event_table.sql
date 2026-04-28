DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool);
DROP FUNCTION IF EXISTS sp_update_event_table(uuid, text, int, text, text, int, bool, int);

CREATE OR REPLACE FUNCTION sp_update_event_table(
    p_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_is_active bool, p_platform_fee_cents int,
    p_row_span int DEFAULT NULL, p_col_span int DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE event_tables SET
        "Label" = COALESCE(p_label, "Label"),
        "Capacity" = COALESCE(p_capacity, "Capacity"),
        "Shape" = COALESCE(p_shape, "Shape"),
        "Color" = CASE WHEN p_color IS NOT NULL THEN p_color ELSE "Color" END,
        "PriceCents" = COALESCE(p_price_cents, "PriceCents"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "PlatformFeeCents" = COALESCE(p_platform_fee_cents, "PlatformFeeCents"),
        "RowSpan" = COALESCE(p_row_span, "RowSpan"),
        "ColSpan" = COALESCE(p_col_span, "ColSpan"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;
