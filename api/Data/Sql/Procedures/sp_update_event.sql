CREATE OR REPLACE FUNCTION sp_update_event(
    p_id uuid, p_title text, p_slug text, p_description text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_max_capacity int, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_grid_rows int, p_grid_cols int, p_venue_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET
        "Title" = COALESCE(p_title, "Title"),
        "Slug" = COALESCE(p_slug, "Slug"),
        "Description" = COALESCE(p_description, "Description"),
        "Category" = CASE WHEN p_category IS NULL THEN "Category"
                           WHEN p_category = '' THEN NULL
                           ELSE p_category END,
        "StartDate" = COALESCE(p_start_date, "StartDate"),
        "EndDate" = COALESCE(p_end_date, "EndDate"),
        "ImagePath" = COALESCE(p_image_path, "ImagePath"),
        "IsFeatured" = COALESCE(p_is_featured, "IsFeatured"),
        "LayoutMode" = COALESCE(p_layout_mode, "LayoutMode"),
        "MaxCapacity" = p_max_capacity,
        "GridRows" = p_grid_rows,
        "GridCols" = p_grid_cols,
        "VenueId" = COALESCE(p_venue_id, "VenueId"),
        "ScheduledPublishAt" = p_scheduled_publish_at,
        "UpdatedAt" = now()
    WHERE "Id" = p_id;
END; $$;