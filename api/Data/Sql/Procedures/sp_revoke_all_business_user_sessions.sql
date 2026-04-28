CREATE OR REPLACE FUNCTION sp_revoke_all_business_user_sessions(
    p_business_user_id uuid, p_except_hash text DEFAULT NULL
) RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_count int;
BEGIN
    UPDATE device_sessions SET "RevokedAt" = now()
    WHERE "BusinessUserId" = p_business_user_id AND "RevokedAt" IS NULL
      AND (p_except_hash IS NULL OR "SessionHash" <> p_except_hash);
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;