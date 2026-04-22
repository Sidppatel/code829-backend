CREATE OR REPLACE FUNCTION sp_remove_event_image(
    p_event_id uuid,
    p_image_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_was_primary boolean;
    v_next_image_id uuid;
BEGIN
    SELECT "IsPrimary" INTO v_was_primary
    FROM event_images
    WHERE "EventId" = p_event_id AND "ImageId" = p_image_id;

    IF v_was_primary IS NULL THEN
        RETURN false;
    END IF;

    DELETE FROM event_images
    WHERE "EventId" = p_event_id AND "ImageId" = p_image_id;

    DELETE FROM images WHERE "Id" = p_image_id;

    IF v_was_primary THEN
        SELECT "ImageId" INTO v_next_image_id
        FROM event_images
        WHERE "EventId" = p_event_id
        ORDER BY "SortOrder" ASC
        LIMIT 1;

        IF v_next_image_id IS NOT NULL THEN
            UPDATE event_images
            SET "IsPrimary" = true, "UpdatedAt" = now()
            WHERE "EventId" = p_event_id AND "ImageId" = v_next_image_id;
        END IF;
    END IF;

    RETURN true;
END; $$;
