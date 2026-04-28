CREATE OR REPLACE FUNCTION sp_search_events(p_query text)
RETURNS TABLE("EventId" uuid) LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT "Id"
      FROM events
     WHERE "Status" = 'Published'
       AND (
           "SearchVector" @@ plainto_tsquery('english', p_query)
           OR similarity("Title", p_query) > 0.1
       );
$$;
