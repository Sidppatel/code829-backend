CREATE OR REPLACE VIEW v_business_user_events AS
SELECT
    aue."Id" AS "BusinessUserEventId",
    aue."BusinessUserId",
    au."FirstName", au."LastName", au."Email",
    au."IsActive" AS "BusinessUserIsActive",
    aue."EventId",
    e."Title" AS "EventTitle", e."Slug" AS "EventSlug",
    e."StartDate", e."EndDate", e."Status" AS "EventStatus",
    aue."AssignedByBusinessUserId",
    aue."CreatedAt", aue."UpdatedAt"
FROM business_user_events aue
JOIN business_users au ON au."Id" = aue."BusinessUserId"
JOIN events e ON e."Id" = aue."EventId";
