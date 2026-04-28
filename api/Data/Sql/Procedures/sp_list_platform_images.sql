CREATE OR REPLACE FUNCTION sp_list_platform_images(p_tag text DEFAULT NULL)
RETURNS SETOF v_platform_images LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT * FROM v_platform_images
    WHERE p_tag IS NULL OR "Tag" = p_tag
    ORDER BY "SortOrder" ASC;
$$;
