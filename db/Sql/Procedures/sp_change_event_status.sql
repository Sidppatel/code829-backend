CREATE OR REPLACE FUNCTION sp_change_event_status(
    p_id uuid, p_status text, p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET
        "Status" = p_status,
        "PublishedAt" = CASE WHEN p_status = 'Published' AND "PublishedAt" IS NULL THEN now() ELSE "PublishedAt" END,
        "ScheduledPublishAt" = p_scheduled_publish_at,
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;