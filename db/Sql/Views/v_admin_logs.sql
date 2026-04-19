CREATE OR REPLACE VIEW v_admin_logs AS
SELECT
    al."Id",
    al."Timestamp",
    al."Action",
    al."AdminUserId",
    au."Email" AS "AdminEmail",
    au."Role"  AS "AdminRole",
    al."EntityType",
    al."EntityId",
    al."Description",
    al."MetadataJson",
    al."IpAddress"
FROM admin_logs al
LEFT JOIN admin_users au ON au."Id" = al."AdminUserId";
