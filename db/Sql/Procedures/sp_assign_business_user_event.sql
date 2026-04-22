CREATE OR REPLACE FUNCTION sp_assign_business_user_event(
    p_business_user_id uuid, p_event_id uuid, p_assigned_by_business_user_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO business_user_events ("BusinessUserId", "EventId", "AssignedByBusinessUserId", "CreatedAt", "UpdatedAt")
    VALUES (p_business_user_id, p_event_id, p_assigned_by_business_user_id, now(), now())
    ON CONFLICT ("BusinessUserId", "EventId") DO UPDATE SET "UpdatedAt" = now()
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
