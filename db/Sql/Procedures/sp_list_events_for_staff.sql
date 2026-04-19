CREATE OR REPLACE FUNCTION sp_list_events_for_staff(
    p_admin_user_id uuid, p_grace_hours int DEFAULT 24
) RETURNS SETOF events LANGUAGE sql STABLE AS $$
    SELECT e.*
    FROM events e
    JOIN admin_user_events aue ON aue."EventId" = e."Id"
    WHERE aue."AdminUserId" = p_admin_user_id
      AND e."Status" IN ('Published', 'Completed')
      AND now() <= e."EndDate" + make_interval(hours => p_grace_hours)
    ORDER BY e."StartDate";
$$;
