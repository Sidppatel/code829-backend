CREATE OR REPLACE VIEW v_feedbacks AS
SELECT
    f."Id"          AS "FeedbackId",
    f."Name",
    f."Email",
    f."Type",
    f."Message",
    f."Rating",
    f."UserId",
    f."UserAgent",
    f."IpAddress",
    f."Diagnostics"::text AS "Diagnostics",
    f."CreatedAt",
    CASE WHEN u."Id" IS NOT NULL
         THEN u."FirstName" || ' ' || u."LastName"
    END              AS "UserFullName"
FROM feedbacks f
LEFT JOIN users u ON u."Id" = f."UserId";
