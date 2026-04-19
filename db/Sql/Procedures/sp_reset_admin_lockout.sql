CREATE OR REPLACE FUNCTION sp_reset_admin_lockout(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET
        "FailedLoginAttempts" = 0,
        "LockedUntil" = NULL,
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;