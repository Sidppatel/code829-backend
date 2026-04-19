CREATE OR REPLACE FUNCTION sp_delete_table(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM tables WHERE "Id" = p_id;
END; $$;