CREATE OR REPLACE FUNCTION sp_update_session_activity(p_session_hash text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE device_sessions SET "LastActivityAt" = now() WHERE "SessionHash" = p_session_hash;
END; $$;