CREATE OR REPLACE FUNCTION sp_revoke_device_session(p_session_hash text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE device_sessions SET "RevokedAt" = now(), "UpdatedAt" = now()
    WHERE "SessionHash" = p_session_hash AND "RevokedAt" IS NULL;
END; $$;