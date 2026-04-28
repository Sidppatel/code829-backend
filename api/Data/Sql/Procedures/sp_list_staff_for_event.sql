CREATE OR REPLACE FUNCTION sp_list_staff_for_event(p_event_id uuid)
RETURNS TABLE(
    "BusinessUserEventId" uuid,
    "BusinessUserId" uuid,
    "FirstName" text,
    "LastName" text,
    "Email" text,
    "BusinessUserIsActive" boolean,
    "EventId" uuid,
    "EventTitle" text,
    "EventSlug" text,
    "StartDate" timestamptz,
    "EndDate" timestamptz,
    "EventStatus" text,
    "AssignedByBusinessUserId" uuid,
    "CreatedAt" timestamptz,
    "UpdatedAt" timestamptz
) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT
        aue."Id", aue."BusinessUserId",
        au."FirstName"::text, au."LastName"::text, au."Email"::text,
        au."IsActive",
        aue."EventId", e."Title"::text, e."Slug"::text,
        e."StartDate", e."EndDate", e."Status"::text,
        aue."AssignedByBusinessUserId",
        aue."CreatedAt", aue."UpdatedAt"
    FROM business_user_events aue
    JOIN business_users au ON au."Id" = aue."BusinessUserId"
    JOIN events e ON e."Id" = aue."EventId"
    WHERE aue."EventId" = p_event_id
      AND au."IsActive" = true
    ORDER BY au."FirstName", au."LastName";
$$;
