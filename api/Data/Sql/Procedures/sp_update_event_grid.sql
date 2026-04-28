CREATE OR REPLACE FUNCTION sp_update_event_grid(p_id uuid, p_grid_rows int, p_grid_cols int)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET "GridRows" = p_grid_rows, "GridCols" = p_grid_cols, "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;