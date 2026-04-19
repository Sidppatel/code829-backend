CREATE OR REPLACE FUNCTION sp_update_admin_password(p_id uuid, p_password_hash text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET "PasswordHash" = p_password_hash, "UpdatedAt" = now() WHERE "Id" = p_id;
END; $$;