CREATE OR REPLACE FUNCTION sp_update_session_activity(p_session_hash text) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_business_user_id uuid;
BEGIN
    UPDATE device_sessions
       SET "LastActivityAt" = now()
     WHERE "SessionHash" = p_session_hash
    RETURNING "BusinessUserId" INTO v_business_user_id;

    IF v_business_user_id IS NOT NULL THEN
        UPDATE business_users SET "LastRequestAt" = now() WHERE "Id" = v_business_user_id;
    END IF;
END; $$;
