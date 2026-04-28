CREATE OR REPLACE VIEW v_user_profile AS
SELECT
    u."Id" AS "UserId", u."Email", u."FirstName", u."LastName",
    u."IsActive", u."LastLoginAt",
    u."Phone", u."OptInLocationEmail", u."HasCompletedOnboarding",
    i."StorageKey" AS "ImageStorageKey", u."CreatedAt",
    a."Line1" AS "AddressLine1",
    a."City", a."State", a."ZipCode"
FROM users u
LEFT JOIN addresses a ON u."AddressId" = a."Id"
LEFT JOIN images i ON u."ImageId" = i."Id";
