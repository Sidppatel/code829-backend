CREATE OR REPLACE VIEW v_admin_users AS
SELECT
    "Id" AS "AdminUserId", "Email", "EmailHash", "FirstName", "LastName",
    "Role", "IsActive", "LastLoginAt", "AvatarPath", "Phone",
    "StripeConnectedAccountId", "CreatedAt", "UpdatedAt"
FROM admin_users;