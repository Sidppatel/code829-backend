CREATE OR REPLACE VIEW v_device_sessions AS
SELECT
    "Id" AS "DeviceSessionId", "UserId", "AdminUserId", "SessionHash",
    "DeviceFingerprint", "DeviceName", "IpAddress",
    "LastActivityAt", "ExpiresAt", "RevokedAt",
    "CreatedAt", "UpdatedAt"
FROM device_sessions;