using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class ProductionConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pgcrypto extension for gen_random_uuid()
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // Drop views before altering underlying tables
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_summary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_pricing_rules;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_tables;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_ticket_types;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events;");

            migrationBuilder.DropForeignKey(
                name: "FK_booking_items_seats_SeatId",
                table: "booking_items");

            migrationBuilder.DropForeignKey(
                name: "FK_booking_items_ticket_types_TicketTypeId",
                table: "booking_items");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_events_EventId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_templates_EventTemplateId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_venue_layouts_VenueLayoutId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_venues_VenueId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_bookings_BookingId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_pricing_rules_pricing_rule_templates_TemplateId",
                table: "pricing_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_pricing_rules_table_types_TableTypeId",
                table: "pricing_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_seat_holds_ticket_types_TicketTypeId",
                table: "seat_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_seat_holds_users_UserId",
                table: "seat_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_venues_VenueId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_types_ticket_type_templates_TemplateId",
                table: "ticket_types");

            migrationBuilder.DropForeignKey(
                name: "FK_users_addresses_AddressId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_layout_tables_table_types_TableTypeId",
                table: "venue_layout_tables");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_layouts_venues_VenueId",
                table: "venue_layouts");

            migrationBuilder.DropForeignKey(
                name: "FK_venues_addresses_AddressId",
                table: "venues");

            migrationBuilder.DropIndex(
                name: "IX_venue_layouts_VenueId",
                table: "venue_layouts");

            migrationBuilder.DropIndex(
                name: "IX_venue_layout_tables_VenueLayoutId",
                table: "venue_layout_tables");

            migrationBuilder.DropIndex(
                name: "IX_seats_TableId",
                table: "seats");

            migrationBuilder.DropIndex(
                name: "IX_seat_holds_SeatId_EventId_IsActive",
                table: "seat_holds");

            migrationBuilder.DropIndex(
                name: "IX_bookings_EventId",
                table: "bookings");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "venues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "venues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "venues",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "venue_layouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "venue_layouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "venue_layouts",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "venue_layout_tables",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "venue_layout_tables",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "venue_layout_tables",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ticket_types",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ticket_types",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ticket_types",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ticket_type_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ticket_type_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ticket_type_templates",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "tables",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "tables",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "tables",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "table_types",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "table_types",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "table_types",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "system_logs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "system_logs",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "seats",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "seats",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "seats",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "seat_holds",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "seat_holds",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "seat_holds",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "pricing_rules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "pricing_rules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "pricing_rules",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "pricing_rule_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "pricing_rule_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "pricing_rule_templates",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "magic_link_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "magic_link_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "magic_link_tokens",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "events",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "event_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "event_templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "event_templates",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "email_logs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "email_logs",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "developer_logs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "developer_logs",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "booking_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "booking_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "booking_items",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "app_settings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "app_settings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "app_settings",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "admin_logs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "admin_logs",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "addresses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "addresses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "addresses",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layouts_VenueId_Name",
                table: "venue_layouts",
                columns: new[] { "VenueId", "Name" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_venue_layouts_EditorMode",
                table: "venue_layouts",
                sql: "\"EditorMode\" IS NULL OR \"EditorMode\" IN ('Grid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_venue_layouts_GridDimensions",
                table: "venue_layouts",
                sql: "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_venue_layouts_LayoutMode",
                table: "venue_layouts",
                sql: "\"LayoutMode\" IN ('None','Grid','CapacityOnly')");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layout_tables_VenueLayoutId_Label",
                table: "venue_layout_tables",
                columns: new[] { "VenueLayoutId", "Label" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_venue_layout_tables_PriceCents",
                table: "venue_layout_tables",
                sql: "\"PriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_venue_layout_tables_PriceType",
                table: "venue_layout_tables",
                sql: "\"PriceType\" IN ('PerTable','PerSeat')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_Role",
                table: "users",
                sql: "\"Role\" IN ('User','Staff','Admin','Developer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ticket_types_PriceCents",
                table: "ticket_types",
                sql: "\"PriceCents\" IS NULL OR \"PriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ticket_types_QuantitySold",
                table: "ticket_types",
                sql: "\"QuantitySold\" >= 0 AND \"QuantitySold\" <= \"QuantityTotal\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ticket_types_QuantityTotal",
                table: "ticket_types",
                sql: "\"QuantityTotal\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ticket_type_templates_DefaultPlatformFeeCents",
                table: "ticket_type_templates",
                sql: "\"DefaultPlatformFeeCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ticket_type_templates_DefaultPriceCents",
                table: "ticket_type_templates",
                sql: "\"DefaultPriceCents\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId_Label",
                table: "tables",
                columns: new[] { "EventId", "Label" },
                unique: true,
                filter: "\"EventId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_Capacity",
                table: "tables",
                sql: "\"Capacity\" IS NULL OR \"Capacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_PriceCents",
                table: "tables",
                sql: "\"PriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_PriceOverrideCents",
                table: "tables",
                sql: "\"PriceOverrideCents\" IS NULL OR \"PriceOverrideCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_PriceType",
                table: "tables",
                sql: "\"PriceType\" IN ('PerTable','PerSeat')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_Shape",
                table: "tables",
                sql: "\"Shape\" IS NULL OR \"Shape\" IN ('Round','Rectangle','Square','Cocktail')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_table_types_DefaultCapacity",
                table: "table_types",
                sql: "\"DefaultCapacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_table_types_DefaultPriceCents",
                table: "table_types",
                sql: "\"DefaultPriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_table_types_DefaultShape",
                table: "table_types",
                sql: "\"DefaultShape\" IN ('Round','Rectangle','Square','Cocktail')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_table_types_PlatformFeeCents",
                table: "table_types",
                sql: "\"PlatformFeeCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_system_logs_Category",
                table: "system_logs",
                sql: "\"Category\" IN ('EntityChange','BackgroundWorker','Cache','MockService','Migration')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_system_logs_DurationMs",
                table: "system_logs",
                sql: "\"DurationMs\" IS NULL OR \"DurationMs\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_seats_TableId_SeatNumber",
                table: "seats",
                columns: new[] { "TableId", "SeatNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_seats_SeatNumber",
                table: "seats",
                sql: "\"SeatNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_IsActive_ExpiresAt",
                table: "seat_holds",
                columns: new[] { "IsActive", "ExpiresAt" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_SeatId_EventId",
                table: "seat_holds",
                columns: new[] { "SeatId", "EventId" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_DateRange",
                table: "pricing_rules",
                sql: "\"ValidFrom\" IS NULL OR \"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_FeeFlatCents",
                table: "pricing_rules",
                sql: "\"FeeFlatCents\" IS NULL OR \"FeeFlatCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_FeePercent",
                table: "pricing_rules",
                sql: "\"FeePercent\" IS NULL OR (\"FeePercent\" >= 0 AND \"FeePercent\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_MaxCount",
                table: "pricing_rules",
                sql: "\"MaxCount\" IS NULL OR \"MaxCount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_PriceCents",
                table: "pricing_rules",
                sql: "\"PriceCents\" IS NULL OR \"PriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_Type",
                table: "pricing_rules",
                sql: "\"Type\" IS NULL OR \"Type\" IN ('Standard','EarlyBird','FirstN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rules_UsedCount",
                table: "pricing_rules",
                sql: "\"UsedCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rule_templates_DefaultPriceCents",
                table: "pricing_rule_templates",
                sql: "\"DefaultPriceCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_pricing_rule_templates_Type",
                table: "pricing_rule_templates",
                sql: "\"Type\" IN ('Standard','EarlyBird','FirstN')");

            migrationBuilder.CreateIndex(
                name: "IX_payments_Status_PaidAt",
                table: "payments",
                columns: new[] { "Status", "PaidAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_AmountCents",
                table: "payments",
                sql: "\"AmountCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_Currency",
                table: "payments",
                sql: "\"Currency\" IN ('usd')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_Status",
                table: "payments",
                sql: "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");

            migrationBuilder.CreateIndex(
                name: "IX_events_Status_StartDate",
                table: "events",
                columns: new[] { "Status", "StartDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_Category",
                table: "events",
                sql: "\"Category\" IS NULL OR \"Category\" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_DateRange",
                table: "events",
                sql: "\"EndDate\" > \"StartDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_EditorMode",
                table: "events",
                sql: "\"EditorMode\" IS NULL OR \"EditorMode\" IN ('Grid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_GridDimensions",
                table: "events",
                sql: "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_LayoutMode",
                table: "events",
                sql: "\"LayoutMode\" IS NULL OR \"LayoutMode\" IN ('None','Grid','CapacityOnly')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_MaxCapacity",
                table: "events",
                sql: "\"MaxCapacity\" IS NULL OR \"MaxCapacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_PlatformFeePercent",
                table: "events",
                sql: "\"PlatformFeePercent\" IS NULL OR (\"PlatformFeePercent\" >= 0 AND \"PlatformFeePercent\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_Status",
                table: "events",
                sql: "\"Status\" IN ('Draft','Published','Completed','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_event_templates_Category",
                table: "event_templates",
                sql: "\"Category\" IS NULL OR \"Category\" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_event_templates_DefaultMaxCapacity",
                table: "event_templates",
                sql: "\"DefaultMaxCapacity\" IS NULL OR \"DefaultMaxCapacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_event_templates_DefaultPlatformFeePercent",
                table: "event_templates",
                sql: "\"DefaultPlatformFeePercent\" IS NULL OR (\"DefaultPlatformFeePercent\" >= 0 AND \"DefaultPlatformFeePercent\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_event_templates_LayoutMode",
                table: "event_templates",
                sql: "\"LayoutMode\" IS NULL OR \"LayoutMode\" IN ('None','Grid','CapacityOnly')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_developer_logs_Severity",
                table: "developer_logs",
                sql: "\"Severity\" IN ('Warning','Error','Critical')");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_EventId_Status",
                table: "bookings",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId_CreatedAt",
                table: "bookings",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_FeeCents",
                table: "bookings",
                sql: "\"FeeCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_Status",
                table: "bookings",
                sql: "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_SubtotalCents",
                table: "bookings",
                sql: "\"SubtotalCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_TotalCents",
                table: "bookings",
                sql: "\"TotalCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_TotalFormula",
                table: "bookings",
                sql: "\"TotalCents\" = \"SubtotalCents\" + \"FeeCents\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_booking_items_PriceCents",
                table: "booking_items",
                sql: "\"PriceCents\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_booking_items_seats_SeatId",
                table: "booking_items",
                column: "SeatId",
                principalTable: "seats",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_booking_items_ticket_types_TicketTypeId",
                table: "booking_items",
                column: "TicketTypeId",
                principalTable: "ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_events_EventId",
                table: "bookings",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_templates_EventTemplateId",
                table: "events",
                column: "EventTemplateId",
                principalTable: "event_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events",
                column: "OrganizerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_events_venue_layouts_VenueLayoutId",
                table: "events",
                column: "VenueLayoutId",
                principalTable: "venue_layouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_events_venues_VenueId",
                table: "events",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_bookings_BookingId",
                table: "payments",
                column: "BookingId",
                principalTable: "bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_rules_pricing_rule_templates_TemplateId",
                table: "pricing_rules",
                column: "TemplateId",
                principalTable: "pricing_rule_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_rules_table_types_TableTypeId",
                table: "pricing_rules",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_seat_holds_ticket_types_TicketTypeId",
                table: "seat_holds",
                column: "TicketTypeId",
                principalTable: "ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_seat_holds_users_UserId",
                table: "seat_holds",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                table: "tables",
                column: "VenueLayoutTableId",
                principalTable: "venue_layout_tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_venues_VenueId",
                table: "tables",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_types_ticket_type_templates_TemplateId",
                table: "ticket_types",
                column: "TemplateId",
                principalTable: "ticket_type_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_addresses_AddressId",
                table: "users",
                column: "AddressId",
                principalTable: "addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_layout_tables_table_types_TableTypeId",
                table: "venue_layout_tables",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_layouts_venues_VenueId",
                table: "venue_layouts",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_venues_addresses_AddressId",
                table: "venues",
                column: "AddressId",
                principalTable: "addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ─── UpdatedAt trigger function ──────────────────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION trigger_set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.""UpdatedAt"" = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
");

            // Apply trigger to all BaseEntity tables
            var tablesWithUpdatedAt = new[] {
                "addresses", "users", "app_settings", "magic_link_tokens",
                "venues", "table_types", "ticket_type_templates", "venue_layouts",
                "venue_layout_tables", "pricing_rule_templates", "event_templates",
                "events", "ticket_types", "tables", "seats", "seat_holds",
                "bookings", "booking_items", "payments", "pricing_rules"
            };
            foreach (var table in tablesWithUpdatedAt)
            {
                migrationBuilder.Sql($@"
CREATE TRIGGER set_updated_at BEFORE UPDATE ON {table}
FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();
");
            }

            // ─── Re-create views ─────────────────────────────────────
            migrationBuilder.Sql(@"
CREATE VIEW v_events AS
SELECT e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status"",
  COALESCE(e.""Category"", et.""Category"") AS ""Category"",
  e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
  COALESCE(e.""LayoutMode"", vl.""LayoutMode"", 'None') AS ""LayoutMode"",
  COALESCE(e.""EditorMode"", vl.""EditorMode"") AS ""EditorMode"",
  COALESCE(e.""GridRows"", vl.""GridRows"") AS ""GridRows"",
  COALESCE(e.""GridCols"", vl.""GridCols"") AS ""GridCols"",
  COALESCE(e.""MaxCapacity"", et.""DefaultMaxCapacity"") AS ""MaxCapacity"",
  COALESCE(e.""PlatformFeePercent"", et.""DefaultPlatformFeePercent"") AS ""PlatformFeePercent"",
  e.""PublishedAt"", e.""ScheduledPublishAt"",
  e.""VenueId"", e.""OrganizerId"", e.""SearchVector"", e.""CreatedAt"", e.""UpdatedAt"",
  v.""Name"" AS ""VenueName"",
  a.""Line1"" AS ""VenueAddress"",
  a.""City"" AS ""VenueCity"",
  a.""State"" AS ""VenueState"",
  a.""ZipCode"" AS ""VenueZipCode""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN venue_layouts vl ON e.""VenueLayoutId"" = vl.""Id""
LEFT JOIN event_templates et ON e.""EventTemplateId"" = et.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_ticket_types AS
SELECT tt.""Id"", tt.""EventId"",
  COALESCE(tt.""Name"", tpl.""Name"") AS ""Name"",
  COALESCE(tt.""Description"", tpl.""Description"") AS ""Description"",
  COALESCE(tt.""PriceCents"", tpl.""DefaultPriceCents"", 0) AS ""PriceCents"",
  COALESCE(tt.""PlatformFeeCents"", tpl.""DefaultPlatformFeeCents"", 0) AS ""PlatformFeeCents"",
  tt.""QuantityTotal"", tt.""QuantitySold"", tt.""SortOrder"",
  tt.""TemplateId"", tt.""CreatedAt"", tt.""UpdatedAt""
FROM ticket_types tt
LEFT JOIN ticket_type_templates tpl ON tt.""TemplateId"" = tpl.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_tables AS
SELECT
    t.""Id"", t.""EventId"", t.""VenueId"", t.""TableTypeId"",
    t.""Label"",
    COALESCE(t.""Capacity"", ttype.""DefaultCapacity"", 0) AS ""Capacity"",
    COALESCE(t.""Shape"", ttype.""DefaultShape"", 'Round') AS ""Shape"",
    COALESCE(t.""Color"", ttype.""DefaultColor"") AS ""Color"",
    t.""Section"", t.""PriceType"",
    COALESCE(t.""PriceOverrideCents"", t.""PriceCents"", ttype.""DefaultPriceCents"", 0) AS ""EffectivePriceCents"",
    COALESCE(ttype.""PlatformFeeCents"", 0) AS ""PlatformFeeCents"",
    t.""IsActive"",
    t.""GridRow"", t.""GridCol"", t.""SortOrder"",
    t.""CreatedAt"", t.""UpdatedAt""
FROM tables t
LEFT JOIN table_types ttype ON t.""TableTypeId"" = ttype.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_pricing_rules AS
SELECT pr.""Id"", pr.""EventId"", pr.""TableTypeId"",
  COALESCE(pr.""Name"", prt.""Name"") AS ""Name"",
  COALESCE(pr.""Type"", prt.""Type"") AS ""Type"",
  COALESCE(pr.""PriceCents"", prt.""DefaultPriceCents"", 0) AS ""PriceCents"",
  pr.""ValidFrom"", pr.""ValidUntil"", pr.""MaxCount"", pr.""UsedCount"", pr.""IsActive"", pr.""SortOrder"",
  COALESCE(pr.""FeePercent"", prt.""DefaultFeePercent"") AS ""FeePercent"",
  COALESCE(pr.""FeeFlatCents"", prt.""DefaultFeeFlatCents"") AS ""FeeFlatCents"",
  COALESCE(pr.""Description"", prt.""Description"") AS ""Description"",
  pr.""TemplateId"", pr.""CreatedAt"", pr.""UpdatedAt""
FROM pricing_rules pr
LEFT JOIN pricing_rule_templates prt ON pr.""TemplateId"" = prt.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_event_summary AS
SELECT e.""Id"", e.""Title"", e.""Slug"", e.""Status"", e.""Category"",
  e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
  v.""Name"" AS ""VenueName"",
  a.""City"" AS ""VenueCity"",
  CONCAT(u.""FirstName"", ' ', u.""LastName"") AS ""OrganizerName"",
  COUNT(DISTINCT tt.""Id"") AS ""TicketTypeCount"",
  COALESCE(SUM(tt.""QuantityTotal""), 0) AS ""TotalCapacity"",
  COALESCE(SUM(tt.""QuantitySold""), 0) AS ""TotalSold""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN ticket_types tt ON tt.""EventId"" = e.""Id""
GROUP BY e.""Id"", e.""Title"", e.""Slug"", e.""Status"", e.""Category"",
  e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
  v.""Name"", a.""City"", u.""FirstName"", u.""LastName"";
");

            // ─── Table comments ──────────────────────────────────────
            migrationBuilder.Sql("COMMENT ON TABLE users IS 'Platform users with role-based access (User, Staff, Admin, Developer)';");
            migrationBuilder.Sql("COMMENT ON TABLE events IS 'Published, draft, or completed events with venue and organizer references';");
            migrationBuilder.Sql("COMMENT ON TABLE venues IS 'Physical locations where events are held';");
            migrationBuilder.Sql("COMMENT ON TABLE bookings IS 'Customer ticket/table reservations with payment tracking';");
            migrationBuilder.Sql("COMMENT ON TABLE payments IS 'Stripe payment records linked 1:1 to bookings';");
            migrationBuilder.Sql("COMMENT ON TABLE tables IS 'Placed table instances on an event floor plan';");
            migrationBuilder.Sql("COMMENT ON TABLE seats IS 'Individual seats at tables, independently bookable';");
            migrationBuilder.Sql("COMMENT ON TABLE seat_holds IS 'Temporary seat reservations during checkout (TTL-based)';");
            migrationBuilder.Sql("COMMENT ON TABLE ticket_types IS 'Ticket tiers/price levels for an event';");
            migrationBuilder.Sql("COMMENT ON TABLE pricing_rules IS 'Pricing rules (standard, early bird, first-N) per event';");
            migrationBuilder.Sql("COMMENT ON TABLE booking_items IS 'Individual line items within a booking (one per ticket/seat)';");
            migrationBuilder.Sql("COMMENT ON TABLE developer_logs IS 'Application error and exception tracking';");
            migrationBuilder.Sql("COMMENT ON TABLE admin_logs IS 'Admin action audit trail';");
            migrationBuilder.Sql("COMMENT ON TABLE system_logs IS 'Entity change audit trail with before/after JSON diffs';");
            migrationBuilder.Sql("COMMENT ON TABLE email_logs IS 'Email delivery tracking';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_booking_items_seats_SeatId",
                table: "booking_items");

            migrationBuilder.DropForeignKey(
                name: "FK_booking_items_ticket_types_TicketTypeId",
                table: "booking_items");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_events_EventId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_templates_EventTemplateId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_venue_layouts_VenueLayoutId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_venues_VenueId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_bookings_BookingId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_pricing_rules_pricing_rule_templates_TemplateId",
                table: "pricing_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_pricing_rules_table_types_TableTypeId",
                table: "pricing_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_seat_holds_ticket_types_TicketTypeId",
                table: "seat_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_seat_holds_users_UserId",
                table: "seat_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_venues_VenueId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_types_ticket_type_templates_TemplateId",
                table: "ticket_types");

            migrationBuilder.DropForeignKey(
                name: "FK_users_addresses_AddressId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_layout_tables_table_types_TableTypeId",
                table: "venue_layout_tables");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_layouts_venues_VenueId",
                table: "venue_layouts");

            migrationBuilder.DropForeignKey(
                name: "FK_venues_addresses_AddressId",
                table: "venues");

            migrationBuilder.DropIndex(
                name: "IX_venue_layouts_VenueId_Name",
                table: "venue_layouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_venue_layouts_EditorMode",
                table: "venue_layouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_venue_layouts_GridDimensions",
                table: "venue_layouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_venue_layouts_LayoutMode",
                table: "venue_layouts");

            migrationBuilder.DropIndex(
                name: "IX_venue_layout_tables_VenueLayoutId_Label",
                table: "venue_layout_tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_venue_layout_tables_PriceCents",
                table: "venue_layout_tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_venue_layout_tables_PriceType",
                table: "venue_layout_tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_users_Role",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ticket_types_PriceCents",
                table: "ticket_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ticket_types_QuantitySold",
                table: "ticket_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ticket_types_QuantityTotal",
                table: "ticket_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ticket_type_templates_DefaultPlatformFeeCents",
                table: "ticket_type_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ticket_type_templates_DefaultPriceCents",
                table: "ticket_type_templates");

            migrationBuilder.DropIndex(
                name: "IX_tables_EventId_Label",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_Capacity",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_PriceCents",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_PriceOverrideCents",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_PriceType",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_Shape",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_table_types_DefaultCapacity",
                table: "table_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_table_types_DefaultPriceCents",
                table: "table_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_table_types_DefaultShape",
                table: "table_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_table_types_PlatformFeeCents",
                table: "table_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_system_logs_Category",
                table: "system_logs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_system_logs_DurationMs",
                table: "system_logs");

            migrationBuilder.DropIndex(
                name: "IX_seats_TableId_SeatNumber",
                table: "seats");

            migrationBuilder.DropCheckConstraint(
                name: "CK_seats_SeatNumber",
                table: "seats");

            migrationBuilder.DropIndex(
                name: "IX_seat_holds_IsActive_ExpiresAt",
                table: "seat_holds");

            migrationBuilder.DropIndex(
                name: "IX_seat_holds_SeatId_EventId",
                table: "seat_holds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_DateRange",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_FeeFlatCents",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_FeePercent",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_MaxCount",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_PriceCents",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_Type",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rules_UsedCount",
                table: "pricing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rule_templates_DefaultPriceCents",
                table: "pricing_rule_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_pricing_rule_templates_Type",
                table: "pricing_rule_templates");

            migrationBuilder.DropIndex(
                name: "IX_payments_Status_PaidAt",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_AmountCents",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_Currency",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_Status",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_events_Status_StartDate",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_Category",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_DateRange",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_EditorMode",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_GridDimensions",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_LayoutMode",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_MaxCapacity",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_PlatformFeePercent",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_Status",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_event_templates_Category",
                table: "event_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_event_templates_DefaultMaxCapacity",
                table: "event_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_event_templates_DefaultPlatformFeePercent",
                table: "event_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_event_templates_LayoutMode",
                table: "event_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_developer_logs_Severity",
                table: "developer_logs");

            migrationBuilder.DropIndex(
                name: "IX_bookings_EventId_Status",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_UserId_CreatedAt",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_FeeCents",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_Status",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_SubtotalCents",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_TotalCents",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_TotalFormula",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_booking_items_PriceCents",
                table: "booking_items");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "venues",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "venues",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "venues",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "venue_layouts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "venue_layouts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "venue_layouts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "venue_layout_tables",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "venue_layout_tables",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "venue_layout_tables",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ticket_types",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ticket_types",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ticket_types",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ticket_type_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ticket_type_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ticket_type_templates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "tables",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "tables",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "tables",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "table_types",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "table_types",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "table_types",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "system_logs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "system_logs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "seats",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "seats",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "seats",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "seat_holds",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "seat_holds",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "seat_holds",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "pricing_rules",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "pricing_rules",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "pricing_rules",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "pricing_rule_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "pricing_rule_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "pricing_rule_templates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "payments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "magic_link_tokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "magic_link_tokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "magic_link_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "events",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "event_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "event_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "event_templates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "email_logs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "email_logs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "developer_logs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "developer_logs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "bookings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "booking_items",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "booking_items",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "booking_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "app_settings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "app_settings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "app_settings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "admin_logs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "admin_logs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "addresses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "addresses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "addresses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layouts_VenueId",
                table: "venue_layouts",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layout_tables_VenueLayoutId",
                table: "venue_layout_tables",
                column: "VenueLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_seats_TableId",
                table: "seats",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_SeatId_EventId_IsActive",
                table: "seat_holds",
                columns: new[] { "SeatId", "EventId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_EventId",
                table: "bookings",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_booking_items_seats_SeatId",
                table: "booking_items",
                column: "SeatId",
                principalTable: "seats",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_booking_items_ticket_types_TicketTypeId",
                table: "booking_items",
                column: "TicketTypeId",
                principalTable: "ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_events_EventId",
                table: "bookings",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_templates_EventTemplateId",
                table: "events",
                column: "EventTemplateId",
                principalTable: "event_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events",
                column: "OrganizerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_events_venue_layouts_VenueLayoutId",
                table: "events",
                column: "VenueLayoutId",
                principalTable: "venue_layouts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_venues_VenueId",
                table: "events",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_bookings_BookingId",
                table: "payments",
                column: "BookingId",
                principalTable: "bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_rules_pricing_rule_templates_TemplateId",
                table: "pricing_rules",
                column: "TemplateId",
                principalTable: "pricing_rule_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_rules_table_types_TableTypeId",
                table: "pricing_rules",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_seat_holds_ticket_types_TicketTypeId",
                table: "seat_holds",
                column: "TicketTypeId",
                principalTable: "ticket_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_seat_holds_users_UserId",
                table: "seat_holds",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                table: "tables",
                column: "VenueLayoutTableId",
                principalTable: "venue_layout_tables",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_venues_VenueId",
                table: "tables",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_types_ticket_type_templates_TemplateId",
                table: "ticket_types",
                column: "TemplateId",
                principalTable: "ticket_type_templates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_addresses_AddressId",
                table: "users",
                column: "AddressId",
                principalTable: "addresses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_venue_layout_tables_table_types_TableTypeId",
                table: "venue_layout_tables",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_venue_layouts_venues_VenueId",
                table: "venue_layouts",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_venues_addresses_AddressId",
                table: "venues",
                column: "AddressId",
                principalTable: "addresses",
                principalColumn: "Id");

            // Drop triggers and function
            var tablesWithUpdatedAt = new[] {
                "addresses", "users", "app_settings", "magic_link_tokens",
                "venues", "table_types", "ticket_type_templates", "venue_layouts",
                "venue_layout_tables", "pricing_rule_templates", "event_templates",
                "events", "ticket_types", "tables", "seats", "seat_holds",
                "bookings", "booking_items", "payments", "pricing_rules"
            };
            foreach (var table in tablesWithUpdatedAt)
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS set_updated_at ON {table};");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trigger_set_updated_at();");
        }
    }
}
