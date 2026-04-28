CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int, p_is_active bool,
    p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE event_ticket_types SET
        "Label" = COALESCE(p_label, "Label"),
        "PriceCents" = COALESCE(p_price_cents, "PriceCents"),
        "PlatformFeeCents" = p_platform_fee_cents,
        "MaxQuantity" = p_max_quantity,
        "SortOrder" = COALESCE(p_sort_order, "SortOrder"),
        "Description" = COALESCE(p_description, "Description"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;