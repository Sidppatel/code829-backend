CREATE OR REPLACE FUNCTION sp_delete_event_table(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    DELETE FROM tables WHERE "EventTableId" = p_id;
    DELETE FROM event_tables WHERE "Id" = p_id;
END; $$;