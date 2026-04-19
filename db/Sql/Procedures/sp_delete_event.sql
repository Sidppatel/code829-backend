CREATE OR REPLACE FUNCTION sp_delete_event(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM events WHERE "Id" = p_id;
END; $$;