CREATE OR REPLACE FUNCTION sp_reorder_venue_images(
    p_venue_id uuid,
    p_image_ids uuid[]
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE i int;
BEGIN
    FOR i IN 1 .. array_length(p_image_ids, 1) LOOP
        UPDATE venue_images
        SET "SortOrder" = i - 1, "UpdatedAt" = now()
        WHERE "VenueId" = p_venue_id AND "ImageId" = p_image_ids[i];
    END LOOP;
END; $$;
