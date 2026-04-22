CREATE OR REPLACE FUNCTION sp_get_primary_image_key(p_entity_type text, p_entity_id uuid)
RETURNS text LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT CASE
        WHEN p_entity_type = 'event' THEN (
            SELECT i."StorageKey"
            FROM event_images ei
            JOIN images i ON i."Id" = ei."ImageId"
            WHERE ei."EventId" = p_entity_id AND ei."IsPrimary" = true
            LIMIT 1
        )
        WHEN p_entity_type = 'venue' THEN (
            SELECT i."StorageKey"
            FROM venue_images vi
            JOIN images i ON i."Id" = vi."ImageId"
            WHERE vi."VenueId" = p_entity_id AND vi."IsPrimary" = true
            LIMIT 1
        )
        ELSE (
            SELECT "StorageKey" FROM images
            WHERE "EntityType" = p_entity_type AND "EntityId" = p_entity_id
            ORDER BY "SortOrder" ASC
            LIMIT 1
        )
    END;
$$;
