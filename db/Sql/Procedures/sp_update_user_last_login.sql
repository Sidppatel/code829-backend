CREATE OR REPLACE FUNCTION sp_update_user_last_login(p_user_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE users SET "LastLoginAt" = now(), "UpdatedAt" = now() WHERE "Id" = p_user_id;
END; $$;