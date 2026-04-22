CREATE OR REPLACE FUNCTION sp_set_platform_primary_image(p_image_id uuid)
RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_exists boolean;
BEGIN
    SELECT EXISTS(SELECT 1 FROM platform_images WHERE "ImageId" = p_image_id)
    INTO v_exists;

    IF NOT v_exists THEN
        RETURN false;
    END IF;

    UPDATE platform_images SET "IsPrimary" = false, "UpdatedAt" = now() WHERE "IsPrimary" = true;
    UPDATE platform_images SET "IsPrimary" = true, "UpdatedAt" = now() WHERE "ImageId" = p_image_id;

    RETURN true;
END; $$;
