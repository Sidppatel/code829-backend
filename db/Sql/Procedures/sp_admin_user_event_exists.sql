CREATE OR REPLACE FUNCTION sp_admin_user_event_exists(
    p_admin_user_id uuid, p_event_id uuid
) RETURNS boolean LANGUAGE sql STABLE AS $$
    SELECT EXISTS(
        SELECT 1 FROM admin_user_events
        WHERE "AdminUserId" = p_admin_user_id AND "EventId" = p_event_id
    );
$$;
