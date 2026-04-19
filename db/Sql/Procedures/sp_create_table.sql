CREATE OR REPLACE FUNCTION sp_create_table(
    p_event_table_id uuid, p_event_id uuid, p_label text,
    p_grid_row int, p_grid_col int, p_sort_order int
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO tables ("Id", "EventTableId", "EventId", "Label", "GridRow", "GridCol",
        "SortOrder", "IsActive", "Status", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), p_event_table_id, p_event_id, p_label,
        p_grid_row, p_grid_col, p_sort_order, true, 'Available', now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;