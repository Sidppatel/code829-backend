CREATE OR REPLACE FUNCTION sp_cleanup_expired_sessions() RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_count int;
BEGIN
    DELETE FROM device_sessions
    WHERE "ExpiresAt" < now()
       OR ("RevokedAt" IS NOT NULL AND "RevokedAt" < now() - interval '7 days');
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;