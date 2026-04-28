CREATE OR REPLACE FUNCTION sp_business_user_event_exists(
    p_business_user_id uuid, p_event_id uuid
) RETURNS boolean LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT EXISTS(
        SELECT 1 FROM business_user_events
        WHERE "BusinessUserId" = p_business_user_id AND "EventId" = p_event_id
    );
$$;
