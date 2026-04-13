using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class FixSpCreateEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // sp_create_event was missing "PublishedAt" in its INSERT column list,
            // causing CK_events_PublishLifecycle violations when seeding Published events.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_event(
    p_title text, p_slug text, p_description text, p_status text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_max_capacity int, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_grid_rows int, p_grid_cols int, p_venue_id uuid, p_organizer_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO events (""Id"", ""Title"", ""Slug"", ""Description"", ""Status"", ""Category"",
        ""StartDate"", ""EndDate"", ""ImagePath"", ""IsFeatured"", ""LayoutMode"",
        ""MaxCapacity"", ""PricePerPersonCents"", ""PlatformFeePercent"", ""PlatformFeeCents"",
        ""GridRows"", ""GridCols"", ""VenueId"", ""OrganizerId"", ""ScheduledPublishAt"",
        ""PublishedAt"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_title, p_slug, p_description, p_status,
        CASE WHEN p_category = '' THEN NULL ELSE p_category END,
        p_start_date, p_end_date, p_image_path, COALESCE(p_is_featured, false), p_layout_mode,
        p_max_capacity, p_price_per_person_cents, p_platform_fee_percent, p_platform_fee_cents,
        p_grid_rows, p_grid_cols, p_venue_id, p_organizer_id, p_scheduled_publish_at,
        CASE WHEN p_status = 'Published' THEN now() ELSE NULL END,
        now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No meaningful rollback — the function body is identical to what Migration 1 intended.
        }
    }
}
