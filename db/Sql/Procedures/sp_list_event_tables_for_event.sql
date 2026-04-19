CREATE OR REPLACE FUNCTION sp_list_event_tables_for_event(p_event_id uuid)
RETURNS SETOF event_tables
LANGUAGE sql STABLE AS $$
    SELECT * FROM event_tables WHERE "EventId" = p_event_id ORDER BY "Label";
$$;