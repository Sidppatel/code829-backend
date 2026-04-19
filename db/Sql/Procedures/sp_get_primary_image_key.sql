CREATE OR REPLACE FUNCTION sp_get_primary_image_key(p_entity_type text, p_entity_id uuid)
RETURNS text LANGUAGE sql STABLE AS $$
    SELECT "StorageKey" FROM images
    WHERE "EntityType" = p_entity_type AND "EntityId" = p_entity_id AND "IsPrimary" = true
    LIMIT 1;
$$;
