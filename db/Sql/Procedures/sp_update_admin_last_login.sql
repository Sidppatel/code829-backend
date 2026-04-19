CREATE OR REPLACE FUNCTION sp_update_admin_last_login(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET "LastLoginAt" = now(), "UpdatedAt" = now() WHERE "Id" = p_id;
END; $$;