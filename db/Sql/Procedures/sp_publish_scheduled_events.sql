CREATE OR REPLACE FUNCTION sp_publish_scheduled_events() RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_count int;
BEGIN
    UPDATE events SET
        "Status" = 'Published', "PublishedAt" = now(),
        "ScheduledPublishAt" = NULL, "UpdatedAt" = now()
    WHERE "Status" = 'Draft'
      AND "ScheduledPublishAt" IS NOT NULL
      AND "ScheduledPublishAt" <= now();
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;