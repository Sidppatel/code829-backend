DROP FUNCTION IF EXISTS sp_publish_scheduled_events();

CREATE OR REPLACE FUNCTION sp_publish_scheduled_events() RETURNS SETOF uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    RETURN QUERY
    UPDATE events SET
        "Status" = 'Published', "PublishedAt" = now(),
        "ScheduledPublishAt" = NULL, "UpdatedAt" = now()
    WHERE "Status" = 'Draft'
      AND "ScheduledPublishAt" IS NOT NULL
      AND "ScheduledPublishAt" <= now()
    RETURNING "Id";
END; $$;
