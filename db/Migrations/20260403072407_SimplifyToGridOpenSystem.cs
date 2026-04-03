using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyToGridOpenSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_event_templates_EventTemplateId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_venue_layouts_VenueLayoutId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                table: "tables");

            migrationBuilder.DropTable(
                name: "booking_items");

            migrationBuilder.DropTable(
                name: "event_templates");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "seat_holds");

            migrationBuilder.DropTable(
                name: "venue_layout_tables");

            migrationBuilder.DropTable(
                name: "pricing_rule_templates");

            migrationBuilder.DropTable(
                name: "seats");

            migrationBuilder.DropTable(
                name: "ticket_types");

            migrationBuilder.DropTable(
                name: "venue_layouts");

            migrationBuilder.DropTable(
                name: "ticket_type_templates");

            migrationBuilder.DropIndex(
                name: "IX_tables_EventId_Label",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_VenueLayoutTableId",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_Capacity",
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

            migrationBuilder.DropIndex(
                name: "IX_table_types_VenueId",
                table: "table_types");

            migrationBuilder.DropCheckConstraint(
                name: "CK_table_types_PlatformFeeCents",
                table: "table_types");

            migrationBuilder.DropIndex(
                name: "IX_events_EventTemplateId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_VenueLayoutId",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_EditorMode",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_LayoutMode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GridCol",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "GridRow",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PriceOverrideCents",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "VenueLayoutTableId",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PlatformFeeCents",
                table: "table_types");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "table_types");

            migrationBuilder.DropColumn(
                name: "EditorMode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EventTemplateId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueLayoutId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "bookings");

            migrationBuilder.AlterColumn<string>(
                name: "Shape",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "tables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Capacity",
                table: "tables",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PosX",
                table: "tables",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PosY",
                table: "tables",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<string>(
                name: "LayoutMode",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricePerPersonCents",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId_Label",
                table: "tables",
                columns: new[] { "EventId", "Label" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_Capacity",
                table: "tables",
                sql: "\"Capacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_Shape",
                table: "tables",
                sql: "\"Shape\" IN ('Round','Rectangle','Square','Cocktail')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_LayoutMode",
                table: "events",
                sql: "\"LayoutMode\" IN ('Grid','Open')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_PricePerPersonCents",
                table: "events",
                sql: "\"PricePerPersonCents\" IS NULL OR \"PricePerPersonCents\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_EventId_Label",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_Capacity",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_Shape",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_LayoutMode",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_events_PricePerPersonCents",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PosX",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PosY",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PricePerPersonCents",
                table: "events");

            migrationBuilder.AlterColumn<string>(
                name: "Shape",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "tables",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Capacity",
                table: "tables",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "GridCol",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridRow",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceOverrideCents",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "tables",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueLayoutTableId",
                table: "tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformFeeCents",
                table: "table_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "table_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LayoutMode",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "EditorMode",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventTemplateId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueLayoutId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "bookings",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DefaultMaxCapacity = table.Column<int>(type: "integer", nullable: true),
                    DefaultPlatformFeePercent = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_templates", x => x.Id);
                    table.CheckConstraint("CK_event_templates_Category", "\"Category\" IS NULL OR \"Category\" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
                    table.CheckConstraint("CK_event_templates_DefaultMaxCapacity", "\"DefaultMaxCapacity\" IS NULL OR \"DefaultMaxCapacity\" > 0");
                    table.CheckConstraint("CK_event_templates_DefaultPlatformFeePercent", "\"DefaultPlatformFeePercent\" IS NULL OR (\"DefaultPlatformFeePercent\" >= 0 AND \"DefaultPlatformFeePercent\" <= 100)");
                    table.CheckConstraint("CK_event_templates_LayoutMode", "\"LayoutMode\" IS NULL OR \"LayoutMode\" IN ('None','Grid','CapacityOnly')");
                });

            migrationBuilder.CreateTable(
                name: "pricing_rule_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DefaultFeeFlatCents = table.Column<int>(type: "integer", nullable: true),
                    DefaultFeePercent = table.Column<int>(type: "integer", nullable: true),
                    DefaultPriceCents = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rule_templates", x => x.Id);
                    table.CheckConstraint("CK_pricing_rule_templates_DefaultPriceCents", "\"DefaultPriceCents\" >= 0");
                    table.CheckConstraint("CK_pricing_rule_templates_Type", "\"Type\" IN ('Standard','EarlyBird','FirstN')");
                });

            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.Id);
                    table.CheckConstraint("CK_seats_SeatNumber", "\"SeatNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_seats_tables_TableId",
                        column: x => x.TableId,
                        principalTable: "tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_type_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DefaultPlatformFeeCents = table.Column<int>(type: "integer", nullable: false),
                    DefaultPriceCents = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_type_templates", x => x.Id);
                    table.CheckConstraint("CK_ticket_type_templates_DefaultPlatformFeeCents", "\"DefaultPlatformFeeCents\" >= 0");
                    table.CheckConstraint("CK_ticket_type_templates_DefaultPriceCents", "\"DefaultPriceCents\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "venue_layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EditorMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GridCols = table.Column<int>(type: "integer", nullable: true),
                    GridRows = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_layouts", x => x.Id);
                    table.CheckConstraint("CK_venue_layouts_EditorMode", "\"EditorMode\" IS NULL OR \"EditorMode\" IN ('Grid')");
                    table.CheckConstraint("CK_venue_layouts_GridDimensions", "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");
                    table.CheckConstraint("CK_venue_layouts_LayoutMode", "\"LayoutMode\" IN ('None','Grid','CapacityOnly')");
                    table.ForeignKey(
                        name: "FK_venue_layouts_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FeeFlatCents = table.Column<int>(type: "integer", nullable: true),
                    FeePercent = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxCount = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.Id);
                    table.CheckConstraint("CK_pricing_rules_DateRange", "\"ValidFrom\" IS NULL OR \"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
                    table.CheckConstraint("CK_pricing_rules_FeeFlatCents", "\"FeeFlatCents\" IS NULL OR \"FeeFlatCents\" >= 0");
                    table.CheckConstraint("CK_pricing_rules_FeePercent", "\"FeePercent\" IS NULL OR (\"FeePercent\" >= 0 AND \"FeePercent\" <= 100)");
                    table.CheckConstraint("CK_pricing_rules_MaxCount", "\"MaxCount\" IS NULL OR \"MaxCount\" > 0");
                    table.CheckConstraint("CK_pricing_rules_PriceCents", "\"PriceCents\" IS NULL OR \"PriceCents\" >= 0");
                    table.CheckConstraint("CK_pricing_rules_Type", "\"Type\" IS NULL OR \"Type\" IN ('Standard','EarlyBird','FirstN')");
                    table.CheckConstraint("CK_pricing_rules_UsedCount", "\"UsedCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_pricing_rules_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pricing_rules_pricing_rule_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "pricing_rule_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pricing_rules_table_types_TableTypeId",
                        column: x => x.TableTypeId,
                        principalTable: "table_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ticket_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PlatformFeeCents = table.Column<int>(type: "integer", nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: true),
                    QuantitySold = table.Column<int>(type: "integer", nullable: false),
                    QuantityTotal = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_types", x => x.Id);
                    table.CheckConstraint("CK_ticket_types_PriceCents", "\"PriceCents\" IS NULL OR \"PriceCents\" >= 0");
                    table.CheckConstraint("CK_ticket_types_QuantitySold", "\"QuantitySold\" >= 0 AND \"QuantitySold\" <= \"QuantityTotal\"");
                    table.CheckConstraint("CK_ticket_types_QuantityTotal", "\"QuantityTotal\" >= 0");
                    table.ForeignKey(
                        name: "FK_ticket_types_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ticket_types_ticket_type_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ticket_type_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "venue_layout_tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TableTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    VenueLayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    GridCol = table.Column<int>(type: "integer", nullable: true),
                    GridRow = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_layout_tables", x => x.Id);
                    table.CheckConstraint("CK_venue_layout_tables_PriceCents", "\"PriceCents\" >= 0");
                    table.CheckConstraint("CK_venue_layout_tables_PriceType", "\"PriceType\" IN ('PerTable','PerSeat')");
                    table.ForeignKey(
                        name: "FK_venue_layout_tables_table_types_TableTypeId",
                        column: x => x.TableTypeId,
                        principalTable: "table_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_venue_layout_tables_venue_layouts_VenueLayoutId",
                        column: x => x.VenueLayoutId,
                        principalTable: "venue_layouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: true),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    GuestEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GuestName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InvitationSent = table.Column<bool>(type: "boolean", nullable: false),
                    InvitationToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsCheckedIn = table.Column<bool>(type: "boolean", nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    QrToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_items", x => x.Id);
                    table.CheckConstraint("CK_booking_items_PriceCents", "\"PriceCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_booking_items_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_items_seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_booking_items_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seat_holds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_holds", x => x.Id);
                    table.CheckConstraint("CK_seat_holds_ExpiresAfterCreate", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_seat_holds_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_holds_seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_holds_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seat_holds_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId_Label",
                table: "tables",
                columns: new[] { "EventId", "Label" },
                unique: true,
                filter: "\"EventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tables_VenueLayoutTableId",
                table: "tables",
                column: "VenueLayoutTableId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_Capacity",
                table: "tables",
                sql: "\"Capacity\" IS NULL OR \"Capacity\" > 0");

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

            migrationBuilder.CreateIndex(
                name: "IX_table_types_VenueId",
                table: "table_types",
                column: "VenueId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_table_types_PlatformFeeCents",
                table: "table_types",
                sql: "\"PlatformFeeCents\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_events_EventTemplateId",
                table: "events",
                column: "EventTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_events_VenueLayoutId",
                table: "events",
                column: "VenueLayoutId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_EditorMode",
                table: "events",
                sql: "\"EditorMode\" IS NULL OR \"EditorMode\" IN ('Grid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_events_LayoutMode",
                table: "events",
                sql: "\"LayoutMode\" IS NULL OR \"LayoutMode\" IN ('None','Grid','CapacityOnly')");

            migrationBuilder.CreateIndex(
                name: "IX_booking_items_BookingId",
                table: "booking_items",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_items_InvitationToken",
                table: "booking_items",
                column: "InvitationToken",
                unique: true,
                filter: "\"InvitationToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_booking_items_QrToken",
                table: "booking_items",
                column: "QrToken",
                unique: true,
                filter: "\"QrToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_booking_items_SeatId",
                table: "booking_items",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_items_TicketTypeId",
                table: "booking_items",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_EventId",
                table: "pricing_rules",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_TableTypeId",
                table: "pricing_rules",
                column: "TableTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_TemplateId",
                table: "pricing_rules",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_EventId",
                table: "seat_holds",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_ExpiresAt",
                table: "seat_holds",
                column: "ExpiresAt");

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

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_TicketTypeId",
                table: "seat_holds",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_UserId",
                table: "seat_holds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_seats_TableId_SeatNumber",
                table: "seats",
                columns: new[] { "TableId", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_EventId",
                table: "ticket_types",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_TemplateId",
                table: "ticket_types",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layout_tables_TableTypeId",
                table: "venue_layout_tables",
                column: "TableTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layout_tables_VenueLayoutId_Label",
                table: "venue_layout_tables",
                columns: new[] { "VenueLayoutId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_layouts_OneDefaultPerVenue",
                table: "venue_layouts",
                column: "VenueId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layouts_VenueId_Name",
                table: "venue_layouts",
                columns: new[] { "VenueId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_templates_EventTemplateId",
                table: "events",
                column: "EventTemplateId",
                principalTable: "event_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_events_venue_layouts_VenueLayoutId",
                table: "events",
                column: "VenueLayoutId",
                principalTable: "venue_layouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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
                name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                table: "tables",
                column: "VenueLayoutTableId",
                principalTable: "venue_layout_tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
