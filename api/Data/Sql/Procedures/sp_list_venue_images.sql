CREATE OR REPLACE FUNCTION sp_list_venue_images(p_venue_id uuid)
RETURNS SETOF v_venue_images LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM v_venue_images
    WHERE "VenueId" = p_venue_id
    ORDER BY "SortOrder" ASC;
$$;
