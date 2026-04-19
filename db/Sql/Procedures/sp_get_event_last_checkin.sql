CREATE OR REPLACE FUNCTION sp_get_event_last_checkin(p_event_id uuid)
RETURNS timestamptz LANGUAGE sql STABLE AS $$
    SELECT MAX("UpdatedAt")
      FROM purchases
     WHERE "EventId" = p_event_id
       AND "Status" = 'CheckedIn';
$$;
