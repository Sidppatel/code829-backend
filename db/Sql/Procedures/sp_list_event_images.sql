CREATE OR REPLACE FUNCTION sp_list_event_images(p_event_id uuid)
RETURNS SETOF v_event_images LANGUAGE sql STABLE AS $$
    SELECT * FROM v_event_images
    WHERE "EventId" = p_event_id
    ORDER BY "SortOrder" ASC;
$$;
