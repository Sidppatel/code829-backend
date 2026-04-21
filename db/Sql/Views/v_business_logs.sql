CREATE OR REPLACE VIEW v_business_logs AS
SELECT
    al."Id",
    al."Timestamp",
    al."Action",
    al."BusinessUserId",
    au."Email" AS "BusinessUserEmail",
    au."Role"  AS "BusinessUserRole",
    al."EntityType",
    al."EntityId",
    al."Description",
    al."MetadataJson",
    al."IpAddress"
FROM business_logs al
LEFT JOIN business_users au ON au."Id" = al."BusinessUserId";
