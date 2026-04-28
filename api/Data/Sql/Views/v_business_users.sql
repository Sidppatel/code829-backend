-- Read projection for the business_users table. StripeConnectedAccountId
-- is no longer included — Connect data was migrated to organizations as
-- part of the Stripe Connect Express rollout. Callers needing acct ids
-- join organizations directly via business_users."OrganizationId".
CREATE OR REPLACE VIEW v_business_users AS
SELECT
    au."Id" AS "BusinessUserId", au."Email", au."EmailHash", au."FirstName", au."LastName",
    au."Role", au."IsActive", au."LastLoginAt",
    i."StorageKey" AS "ImageStorageKey",
    au."Phone", au."CreatedAt", au."UpdatedAt"
FROM business_users au
LEFT JOIN images i ON au."ImageId" = i."Id";
