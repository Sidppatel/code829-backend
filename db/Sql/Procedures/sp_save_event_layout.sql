CREATE OR REPLACE FUNCTION sp_save_event_layout(
    p_event_id uuid, p_grid_rows int, p_grid_cols int,
    p_tables jsonb, p_locked_ids uuid[]
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_request_ids uuid[];
    v_table jsonb;
    v_id uuid;
BEGIN
    UPDATE events SET "GridRows" = p_grid_rows, "GridCols" = p_grid_cols, "UpdatedAt" = now()
    WHERE "Id" = p_event_id;

    SELECT COALESCE(array_agg((t->>'Id')::uuid) FILTER (WHERE t->>'Id' IS NOT NULL), '{}')
    INTO v_request_ids
    FROM jsonb_array_elements(p_tables) AS t;

    DELETE FROM tables
    WHERE "EventId" = p_event_id
      AND "Id" <> ALL(v_request_ids)
      AND "Id" <> ALL(p_locked_ids);

    FOR v_table IN SELECT * FROM jsonb_array_elements(p_tables)
    LOOP
        v_id := NULLIF(v_table->>'Id', '')::uuid;
        IF v_id IS NOT NULL AND v_id = ANY(p_locked_ids) THEN
            CONTINUE;
        END IF;

        IF v_id IS NOT NULL AND EXISTS(SELECT 1 FROM tables WHERE "Id" = v_id) THEN
            UPDATE tables SET
                "Label" = v_table->>'Label',
                "GridRow" = (v_table->>'GridRow')::int,
                "GridCol" = (v_table->>'GridCol')::int,
                "IsActive" = (v_table->>'IsActive')::bool,
                "SortOrder" = (v_table->>'SortOrder')::int,
                "EventTableId" = (v_table->>'EventTableId')::uuid,
                "UpdatedAt" = now()
            WHERE "Id" = v_id;
        ELSE
            INSERT INTO tables ("Id", "EventId", "EventTableId", "Label",
                "GridRow", "GridCol", "IsActive", "SortOrder", "Status",
                "CreatedAt", "UpdatedAt")
            VALUES (
                COALESCE(v_id, gen_random_uuid()), p_event_id,
                (v_table->>'EventTableId')::uuid,
                v_table->>'Label',
                (v_table->>'GridRow')::int,
                (v_table->>'GridCol')::int,
                (v_table->>'IsActive')::bool,
                (v_table->>'SortOrder')::int,
                'Available', now(), now()
            );
        END IF;
    END LOOP;
END; $$;