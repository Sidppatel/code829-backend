using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTypeDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "event_ticket_types",
                type: "text",
                nullable: true);

            // Update stored procedures
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_event_ticket_type(
    p_event_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int,
    p_description text DEFAULT NULL
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_ticket_types (""Id"", ""EventId"", ""Label"", ""PriceCents"", ""PlatformFeeCents"",
        ""MaxQuantity"", ""SortOrder"", ""Description"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_event_id, p_label, p_price_cents, p_platform_fee_cents,
        p_max_quantity, p_sort_order, p_description, true, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_event_ticket_type(
    p_id uuid, p_label text, p_price_cents int,
    p_platform_fee_cents int, p_max_quantity int, p_sort_order int, p_is_active bool,
    p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE event_ticket_types SET
        ""Label"" = COALESCE(p_label, ""Label""),
        ""PriceCents"" = COALESCE(p_price_cents, ""PriceCents""),
        ""PlatformFeeCents"" = p_platform_fee_cents,
        ""MaxQuantity"" = p_max_quantity,
        ""SortOrder"" = COALESCE(p_sort_order, ""SortOrder""),
        ""Description"" = COALESCE(p_description, ""Description""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            // Update view
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_ticket_types_summary;");
            migrationBuilder.Sql(@"
CREATE VIEW v_event_ticket_types_summary AS
SELECT
    ett.""Id"", ett.""EventId"", ett.""Label"", ett.""PriceCents"",
    ett.""PlatformFeeCents"", ett.""MaxQuantity"", ett.""SortOrder"", ett.""IsActive"",
    ett.""Description"",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "event_ticket_types");
            
            // Revert SPs (back to original signatures)
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

            // Revert view
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_ticket_types_summary;");
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
        }
    }
}
