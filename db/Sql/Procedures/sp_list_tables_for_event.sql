CREATE OR REPLACE FUNCTION sp_list_tables_for_event(p_event_id uuid)
RETURNS SETOF tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM tables WHERE "EventId" = p_event_id ORDER BY "SortOrder";
$$;