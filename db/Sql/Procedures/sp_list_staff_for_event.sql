CREATE OR REPLACE FUNCTION sp_list_staff_for_event(p_event_id uuid)
RETURNS TABLE(
    "AdminUserEventId" uuid,
    "AdminUserId" uuid,
    "FirstName" text,
    "LastName" text,
    "Email" text,
    "AdminUserIsActive" boolean,
    "EventId" uuid,
    "EventTitle" text,
    "EventSlug" text,
    "StartDate" timestamptz,
    "EndDate" timestamptz,
    "EventStatus" text,
    "AssignedByAdminUserId" uuid,
    "CreatedAt" timestamptz,
    "UpdatedAt" timestamptz
) LANGUAGE sql STABLE AS $$
    SELECT
        aue."Id", aue."AdminUserId",
        au."FirstName"::text, au."LastName"::text, au."Email"::text,
        au."IsActive",
        aue."EventId", e."Title"::text, e."Slug"::text,
        e."StartDate", e."EndDate", e."Status"::text,
        aue."AssignedByAdminUserId",
        aue."CreatedAt", aue."UpdatedAt"
    FROM admin_user_events aue
    JOIN admin_users au ON au."Id" = aue."AdminUserId"
    JOIN events e ON e."Id" = aue."EventId"
    WHERE aue."EventId" = p_event_id
      AND au."IsActive" = true
    ORDER BY au."FirstName", au."LastName";
$$;
