CREATE OR REPLACE VIEW v_business_users AS
SELECT
    au."Id" AS "BusinessUserId", au."Email", au."EmailHash", au."FirstName", au."LastName",
    au."Role", au."IsActive", au."LastLoginAt",
    i."StorageKey" AS "ImageStorageKey",
    au."Phone", au."StripeConnectedAccountId", au."CreatedAt", au."UpdatedAt"
FROM business_users au
LEFT JOIN images i ON au."ImageId" = i."Id";
