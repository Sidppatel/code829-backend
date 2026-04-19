CREATE OR REPLACE VIEW v_admin_user_events AS
SELECT
    aue."Id" AS "AdminUserEventId",
    aue."AdminUserId",
    au."FirstName", au."LastName", au."Email",
    au."IsActive" AS "AdminUserIsActive",
    aue."EventId",
    e."Title" AS "EventTitle", e."Slug" AS "EventSlug",
    e."StartDate", e."EndDate", e."Status" AS "EventStatus",
    aue."AssignedByAdminUserId",
    aue."CreatedAt", aue."UpdatedAt"
FROM admin_user_events aue
JOIN admin_users au ON au."Id" = aue."AdminUserId"
JOIN events e ON e."Id" = aue."EventId";
