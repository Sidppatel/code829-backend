CREATE OR REPLACE FUNCTION sp_increment_business_user_failed_login(
    p_id uuid, p_max_attempts int, p_lockout_minutes int
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_users SET
        "FailedLoginAttempts" = "FailedLoginAttempts" + 1,
        "LockedUntil" = CASE
            WHEN "FailedLoginAttempts" + 1 >= p_max_attempts
                THEN now() + (p_lockout_minutes::text || ' minutes')::interval
            ELSE "LockedUntil"
        END,
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;