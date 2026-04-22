CREATE OR REPLACE FUNCTION sp_update_table(
    p_id uuid, p_label text, p_event_table_id uuid,
    p_grid_row int, p_grid_col int, p_is_active bool, p_sort_order int
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE tables SET
        "Label" = COALESCE(p_label, "Label"),
        "EventTableId" = COALESCE(p_event_table_id, "EventTableId"),
        "GridRow" = COALESCE(p_grid_row, "GridRow"),
        "GridCol" = COALESCE(p_grid_col, "GridCol"),
        "IsActive" = COALESCE(p_is_active, "IsActive"),
        "SortOrder" = COALESCE(p_sort_order, "SortOrder"),
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;