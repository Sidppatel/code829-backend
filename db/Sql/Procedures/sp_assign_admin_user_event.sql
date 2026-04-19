CREATE OR REPLACE FUNCTION sp_assign_admin_user_event(
    p_admin_user_id uuid, p_event_id uuid, p_assigned_by_admin_user_id uuid DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO admin_user_events ("AdminUserId", "EventId", "AssignedByAdminUserId", "CreatedAt", "UpdatedAt")
    VALUES (p_admin_user_id, p_event_id, p_assigned_by_admin_user_id, now(), now())
    ON CONFLICT ("AdminUserId", "EventId") DO UPDATE SET "UpdatedAt" = now()
    RETURNING "Id" INTO v_id;
    RETURN v_id;
END; $$;
