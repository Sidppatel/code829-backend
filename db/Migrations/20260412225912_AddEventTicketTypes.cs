using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTicketTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventTicketTypeId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_ticket_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    PlatformFeeCents = table.Column<int>(type: "integer", nullable: true),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_ticket_types", x => x.Id);
                    table.CheckConstraint("CK_event_ticket_types_MaxQuantity", "\"MaxQuantity\" IS NULL OR \"MaxQuantity\" > 0");
                    table.CheckConstraint("CK_event_ticket_types_PriceCents", "\"PriceCents\" >= 0");
                    table.CheckConstraint("CK_event_ticket_types_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_event_ticket_types_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_EventTicketTypeId",
                table: "bookings",
                column: "EventTicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_event_ticket_types_EventId_Label",
                table: "event_ticket_types",
                columns: new[] { "EventId", "Label" });

            migrationBuilder.CreateIndex(
                name: "IX_event_ticket_types_EventId_SortOrder",
                table: "event_ticket_types",
                columns: new[] { "EventId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_event_ticket_types_EventTicketTypeId",
                table: "bookings",
                column: "EventTicketTypeId",
                principalTable: "event_ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ─── New view: v_event_ticket_types_summary ─────────────
            migrationBuilder.Sql(@"
CREATE VIEW v_event_ticket_types_summary AS
SELECT
    ett.""Id"", ett.""EventId"", ett.""Label"", ett.""PriceCents"",
    ett.""PlatformFeeCents"", ett.""MaxQuantity"", ett.""SortOrder"", ett.""IsActive"",
    COALESCE(bs.sold, 0)::int AS ""SoldCount"",
    CASE
        WHEN ett.""MaxQuantity"" IS NULL THEN -1
        ELSE GREATEST(0, ett.""MaxQuantity"" - COALESCE(bs.sold, 0))
    END::int AS ""AvailableCount""
FROM event_ticket_types ett
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b.""SeatsReserved""), 0)::int AS sold
    FROM bookings b
    WHERE b.""EventTicketTypeId"" = ett.""Id""
      AND b.""Status"" IN ('Pending', 'Paid', 'CheckedIn')
) bs ON true;
");

            // ─── Update v_bookings to include ticket type ───────────
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_bookings;");
            migrationBuilder.Sql(@"
CREATE VIEW v_bookings AS
SELECT
    b.""Id"", b.""BookingNumber"", b.""Status""::text,
    b.""SubtotalCents"", b.""FeeCents"", b.""TotalCents"",
    b.""QrToken"", b.""SeatsReserved"", b.""CreatedAt"",
    b.""UserId"",
    u.""Email"" AS ""UserEmail"",
    u.""FirstName"" AS ""UserFirstName"",
    u.""LastName"" AS ""UserLastName"",
    b.""EventId"",
    e.""Title"" AS ""EventTitle"",
    e.""Slug"" AS ""EventSlug"",
    e.""StartDate"" AS ""EventStartDate"",
    e.""EndDate"" AS ""EventEndDate"",
    COALESCE(e.""Category""::text, '') AS ""EventCategory"",
    e.""ImagePath"" AS ""EventImagePath"",
    v.""Name"" AS ""VenueName"",
    COALESCE(addr.""Line1"", '') AS ""VenueAddress"",
    COALESCE(addr.""City"", '') AS ""VenueCity"",
    COALESCE(addr.""State"", '') AS ""VenueState"",
    b.""TableId"",
    tbl.""Label"" AS ""TableLabel"",
    b.""EventTicketTypeId"",
    ett.""Label"" AS ""EventTicketTypeLabel"",
    p.""Id"" AS ""PaymentId"",
    p.""PaymentIntentId"",
    p.""Status""::text AS ""PaymentStatus"",
    p.""AmountCents"" AS ""PaymentAmountCents"",
    p.""PaidAt"", p.""RefundedAt"",
    COALESCE(tc.cnt, 0)::int AS ""TicketCount"",
    e.""OrganizerId""
FROM bookings b
JOIN users u ON b.""UserId"" = u.""Id""
JOIN events e ON b.""EventId"" = e.""Id""
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses addr ON v.""AddressId"" = addr.""Id""
LEFT JOIN tables tbl ON b.""TableId"" = tbl.""Id""
LEFT JOIN event_ticket_types ett ON b.""EventTicketTypeId"" = ett.""Id""
LEFT JOIN payments p ON p.""BookingId"" = b.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM booking_tickets bt WHERE bt.""BookingId"" = b.""Id""
) tc ON true;
");

            // ─── Update v_events to include MinTicketTypePriceCents ──
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events;");
            migrationBuilder.Sql(@"
CREATE VIEW v_events AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    e.""LayoutMode""::text, e.""MaxCapacity"", e.""PricePerPersonCents"",
    e.""PlatformFeePercent"", e.""PlatformFeeCents"",
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
    COALESCE(u.""FirstName"", '') AS ""OrganizerFirstName"",
    COALESCE(u.""LastName"", '') AS ""OrganizerLastName"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
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
) ettp ON true;
");

            // ─── Update v_event_summary to include MinTicketTypePriceCents ──
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_summary;");
            migrationBuilder.Sql(@"
CREATE VIEW v_event_summary AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"",
    img.""StorageKey"" AS ""PrimaryImageKey"",
    e.""IsFeatured"",
    e.""LayoutMode""::text, e.""PricePerPersonCents"", e.""MaxCapacity"",
    e.""VenueId"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    e.""OrganizerId"",
    COALESCE(u.""FirstName"" || ' ' || u.""LastName"", '') AS ""OrganizerName"",
    COALESCE(e.""MaxCapacity"", 0)::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents"",
    e.""CreatedAt""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN LATERAL (
    SELECT ""StorageKey""
    FROM images
    WHERE ""EntityType"" = 'event' AND ""EntityId"" = e.""Id"" AND ""IsPrimary"" = true
    LIMIT 1
) img ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
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
) ettp ON true;
");

            // ─── Stored procedures for EventTicketType ──────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_event_ticket_type(
    p_event_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_ticket_types (""Id"", ""EventId"", ""Label"", ""PriceCents"", ""PlatformFeeCents"",
        ""MaxQuantity"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_event_id, p_label, p_price_cents, p_platform_fee_cents,
        p_max_quantity, p_sort_order, true, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int, p_is_active bool
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE event_ticket_types SET
        ""Label"" = COALESCE(p_label, ""Label""),
        ""PriceCents"" = COALESCE(p_price_cents, ""PriceCents""),
        ""PlatformFeeCents"" = p_platform_fee_cents,
        ""MaxQuantity"" = p_max_quantity,
        ""SortOrder"" = COALESCE(p_sort_order, ""SortOrder""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_event_ticket_type(p_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE event_ticket_types SET ""IsActive"" = false, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            // ─── Update sp_create_booking to accept EventTicketTypeId ──
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_booking(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_event_ticket_type_id uuid,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_booking_number text, p_status text DEFAULT 'Pending'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO bookings (""Id"", ""BookingNumber"", ""Status"", ""UserId"", ""EventId"", ""TableId"",
        ""SeatsReserved"", ""EventTicketTypeId"", ""SubtotalCents"", ""FeeCents"", ""TotalCents"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new view
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_ticket_types_summary;");

            // Drop new SPs
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_event_ticket_type(uuid, text, int, int, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_event_ticket_type(uuid, text, int, int, int, int, bool);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_delete_event_ticket_type(uuid);");

            // Restore original v_bookings (without EventTicketTypeId columns)
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_bookings;");
            migrationBuilder.Sql(@"
CREATE VIEW v_bookings AS
SELECT
    b.""Id"", b.""BookingNumber"", b.""Status""::text,
    b.""SubtotalCents"", b.""FeeCents"", b.""TotalCents"",
    b.""QrToken"", b.""SeatsReserved"", b.""CreatedAt"",
    b.""UserId"",
    u.""Email"" AS ""UserEmail"",
    u.""FirstName"" AS ""UserFirstName"",
    u.""LastName"" AS ""UserLastName"",
    b.""EventId"",
    e.""Title"" AS ""EventTitle"",
    e.""Slug"" AS ""EventSlug"",
    e.""StartDate"" AS ""EventStartDate"",
    e.""EndDate"" AS ""EventEndDate"",
    COALESCE(e.""Category""::text, '') AS ""EventCategory"",
    e.""ImagePath"" AS ""EventImagePath"",
    v.""Name"" AS ""VenueName"",
    COALESCE(addr.""Line1"", '') AS ""VenueAddress"",
    COALESCE(addr.""City"", '') AS ""VenueCity"",
    COALESCE(addr.""State"", '') AS ""VenueState"",
    b.""TableId"",
    tbl.""Label"" AS ""TableLabel"",
    p.""Id"" AS ""PaymentId"",
    p.""PaymentIntentId"",
    p.""Status""::text AS ""PaymentStatus"",
    p.""AmountCents"" AS ""PaymentAmountCents"",
    p.""PaidAt"", p.""RefundedAt"",
    COALESCE(tc.cnt, 0)::int AS ""TicketCount"",
    e.""OrganizerId""
FROM bookings b
JOIN users u ON b.""UserId"" = u.""Id""
JOIN events e ON b.""EventId"" = e.""Id""
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses addr ON v.""AddressId"" = addr.""Id""
LEFT JOIN tables tbl ON b.""TableId"" = tbl.""Id""
LEFT JOIN payments p ON p.""BookingId"" = b.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM booking_tickets bt WHERE bt.""BookingId"" = b.""Id""
) tc ON true;
");

            // Restore original v_events (without MinTicketTypePriceCents)
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events;");
            migrationBuilder.Sql(@"
CREATE VIEW v_events AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    e.""LayoutMode""::text, e.""MaxCapacity"", e.""PricePerPersonCents"",
    e.""PlatformFeePercent"", e.""PlatformFeeCents"",
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
    COALESCE(u.""FirstName"", '') AS ""OrganizerFirstName"",
    COALESCE(u.""LastName"", '') AS ""OrganizerLastName"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true;
");

            // Restore original v_event_summary (without MinTicketTypePriceCents)
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_summary;");
            migrationBuilder.Sql(@"
CREATE VIEW v_event_summary AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Status""::text,
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"",
    img.""StorageKey"" AS ""PrimaryImageKey"",
    e.""IsFeatured"",
    e.""LayoutMode""::text, e.""PricePerPersonCents"", e.""MaxCapacity"",
    e.""VenueId"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    e.""OrganizerId"",
    COALESCE(u.""FirstName"" || ' ' || u.""LastName"", '') AS ""OrganizerName"",
    COALESCE(e.""MaxCapacity"", 0)::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    e.""CreatedAt""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN LATERAL (
    SELECT ""StorageKey""
    FROM images
    WHERE ""EntityType"" = 'event' AND ""EntityId"" = e.""Id"" AND ""IsPrimary"" = true
    LIMIT 1
) img ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS sold
    FROM bookings b
    WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et.""PriceCents"") AS min_price
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true AND t.""Status"" = 'Available'
) ts ON true;
");

            // Restore original sp_create_booking (without EventTicketTypeId)
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_booking(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_seats int,
    p_subtotal_cents int, p_fee_cents int, p_total_cents int,
    p_booking_number text, p_status text DEFAULT 'Pending'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO bookings (""Id"", ""BookingNumber"", ""Status"", ""UserId"", ""EventId"", ""TableId"",
        ""SeatsReserved"", ""SubtotalCents"", ""FeeCents"", ""TotalCents"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_subtotal_cents, p_fee_cents, p_total_cents, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_event_ticket_types_EventTicketTypeId",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "event_ticket_types");

            migrationBuilder.DropIndex(
                name: "IX_bookings_EventTicketTypeId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "EventTicketTypeId",
                table: "bookings");
        }
    }
}
