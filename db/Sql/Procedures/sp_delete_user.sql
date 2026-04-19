CREATE OR REPLACE FUNCTION sp_delete_user(p_user_id uuid)
RETURNS bool LANGUAGE plpgsql AS $$
DECLARE
    v_found bool;
BEGIN
    DELETE FROM users WHERE "Id" = p_user_id
    RETURNING true INTO v_found;

    RETURN COALESCE(v_found, false);
END; $$;
