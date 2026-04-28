CREATE OR REPLACE FUNCTION sp_cleanup_expired_locks() RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_count int;
BEGIN
    UPDATE tables SET "Status" = 'Available', "LockedByUserId" = NULL,
        "LockExpiresAt" = NULL, "UpdatedAt" = now()
    WHERE "Status" = 'Locked' AND "LockExpiresAt" < now();
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;