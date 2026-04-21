CREATE OR REPLACE VIEW v_invitations AS
SELECT
    i."Id" AS "InvitationId", i."Email", i."TokenHash", i."Role",
    i."InvitedByBusinessUserId", i."Status",
    i."ExpiresAt", i."AcceptedAt",
    i."CreatedAt", i."UpdatedAt",
    a."FirstName" AS "InviterFirstName",
    a."LastName" AS "InviterLastName"
FROM invitations i
JOIN business_users a ON i."InvitedByBusinessUserId" = a."Id";
