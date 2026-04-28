CREATE OR REPLACE FUNCTION sp_create_business_user_device_session(
    p_business_user_id uuid, p_session_hash text, p_fingerprint text,
    p_device_name text, p_ip text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO device_sessions ("BusinessUserId", "SessionHash", "DeviceFingerprint",
        "DeviceName", "IpAddress", "LastActivityAt", "ExpiresAt", "CreatedAt", "UpdatedAt")
    VALUES (p_business_user_id, p_session_hash, p_fingerprint,
        p_device_name, p_ip, now(), p_expires_at, now(), now())
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;