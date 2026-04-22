CREATE OR REPLACE FUNCTION sp_set_user_active(p_user_id uuid, p_is_active bool)
RETURNS bool LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_found bool;
BEGIN
    UPDATE users SET "IsActive" = p_is_active, "UpdatedAt" = now()
    WHERE "Id" = p_user_id
    RETURNING true INTO v_found;

    RETURN COALESCE(v_found, false);
END; $$;
