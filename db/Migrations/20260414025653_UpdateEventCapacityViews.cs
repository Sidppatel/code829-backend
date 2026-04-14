using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEventCapacityViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_summary CASCADE;");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_events AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    e.""LayoutMode""::text, e.""MaxCapacity"",
    ettp.min_price::int AS ""PricePerPersonCents"",
    e.""GridRows"", e.""GridCols"", e.""PublishedAt"", e.""ScheduledPublishAt"",
    e.""VenueId"", e.""OrganizerId"",
    e.""CreatedAt"", e.""UpdatedAt"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""Line1"", '') AS ""VenueAddress"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    COALESCE(a.""ZipCode"", '') AS ""VenueZipCode"",
    v.""Description"" AS ""VenueDescription"",
    v.""ImagePath"" AS ""VenueImagePath"",
    v.""Phone"" AS ""VenuePhone"",
    v.""Email"" AS ""VenueEmail"",
    v.""Website"" AS ""VenueWebsite"",
    v.""IsActive"" AS ""VenueIsActive"",
    v.""CreatedAt"" AS ""VenueCreatedAt"",
    COALESCE(au.""FirstName"", '') AS ""OrganizerFirstName"",
    COALESCE(au.""LastName"", '') AS ""OrganizerLastName"",
    COALESCE(e.""MaxCapacity"", ett_cap.total_qty, 0)::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN admin_users au ON e.""OrganizerId"" = au.""Id""
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b.""SeatsReserved""), COUNT(*))::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.""PriceCents"") AS min_price
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ettp ON true
LEFT JOIN LATERAL (
    SELECT SUM(ett.""MaxQuantity"") AS total_qty
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ett_cap ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_event_summary AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"",
    img.""StorageKey"" AS ""PrimaryImageKey"",
    e.""IsFeatured"",
    e.""LayoutMode""::text,
    ettp.min_price::int AS ""PricePerPersonCents"",
    e.""MaxCapacity"",
    e.""VenueId"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    e.""OrganizerId"",
    COALESCE(au.""FirstName"" || ' ' || au.""LastName"", '') AS ""OrganizerName"",
    COALESCE(e.""MaxCapacity"", ett_cap.total_qty, 0)::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents"",
    e.""CreatedAt""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN admin_users au ON e.""OrganizerId"" = au.""Id""
LEFT JOIN LATERAL (
    SELECT ""StorageKey""
    FROM images
    WHERE ""EntityType"" = 'event' AND ""EntityId"" = e.""Id"" AND ""IsPrimary"" = true
    LIMIT 1
) img ON true
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b.""SeatsReserved""), COUNT(*))::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett.""PriceCents"") AS min_price
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ettp ON true
LEFT JOIN LATERAL (
    SELECT SUM(ett.""MaxQuantity"") AS total_qty
    FROM event_ticket_types ett
    WHERE ett.""EventId"" = e.""Id"" AND ett.""IsActive"" = true
) ett_cap ON true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
