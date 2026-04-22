CREATE OR REPLACE FUNCTION sp_staff_can_access_event(
    p_business_user_id uuid, p_event_id uuid, p_grace_hours int DEFAULT 24
) RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1
        FROM business_user_events aue
        JOIN business_users au ON au."Id" = aue."BusinessUserId"
        JOIN events e ON e."Id" = aue."EventId"
        WHERE aue."BusinessUserId" = p_business_user_id
          AND aue."EventId" = p_event_id
          AND au."IsActive" = true
          AND now() <= e."EndDate" + make_interval(hours => p_grace_hours)
    );
$$;
