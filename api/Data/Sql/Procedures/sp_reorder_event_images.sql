CREATE OR REPLACE FUNCTION sp_reorder_event_images(
    p_event_id uuid,
    p_image_ids uuid[]
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    i int;
BEGIN
    FOR i IN 1 .. array_length(p_image_ids, 1) LOOP
        UPDATE event_images
        SET "SortOrder" = i - 1, "UpdatedAt" = now()
        WHERE "EventId" = p_event_id AND "ImageId" = p_image_ids[i];
    END LOOP;
END; $$;
