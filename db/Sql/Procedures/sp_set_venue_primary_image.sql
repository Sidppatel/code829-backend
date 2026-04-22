CREATE OR REPLACE FUNCTION sp_set_venue_primary_image(
    p_venue_id uuid,
    p_image_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_exists boolean;
BEGIN
    SELECT EXISTS(
        SELECT 1 FROM venue_images WHERE "VenueId" = p_venue_id AND "ImageId" = p_image_id
    ) INTO v_exists;

    IF NOT v_exists THEN
        RETURN false;
    END IF;

    UPDATE venue_images
    SET "IsPrimary" = false, "UpdatedAt" = now()
    WHERE "VenueId" = p_venue_id AND "IsPrimary" = true;

    UPDATE venue_images
    SET "IsPrimary" = true, "UpdatedAt" = now()
    WHERE "VenueId" = p_venue_id AND "ImageId" = p_image_id;

    RETURN true;
END; $$;
