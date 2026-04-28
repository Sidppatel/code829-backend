CREATE OR REPLACE FUNCTION sp_reset_business_user_lockout(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_users SET
        "FailedLoginAttempts" = 0,
        "LockedUntil" = NULL,
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;