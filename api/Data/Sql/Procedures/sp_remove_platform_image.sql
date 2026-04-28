CREATE OR REPLACE FUNCTION sp_remove_platform_image(p_image_id uuid)
RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_was_primary boolean;
    v_next_image_id uuid;
BEGIN
    SELECT "IsPrimary" INTO v_was_primary
    FROM platform_images WHERE "ImageId" = p_image_id;

    IF v_was_primary IS NULL THEN
        RETURN false;
    END IF;

    DELETE FROM platform_images WHERE "ImageId" = p_image_id;
    DELETE FROM images WHERE "Id" = p_image_id;

    IF v_was_primary THEN
        SELECT "ImageId" INTO v_next_image_id
        FROM platform_images
        ORDER BY "SortOrder" ASC
        LIMIT 1;

        IF v_next_image_id IS NOT NULL THEN
            UPDATE platform_images
            SET "IsPrimary" = true, "UpdatedAt" = now()
            WHERE "ImageId" = v_next_image_id;
        END IF;
    END IF;

    RETURN true;
END; $$;
