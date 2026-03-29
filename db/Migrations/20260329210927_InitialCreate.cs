using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EncryptedValue = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "developer_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developer_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultMaxCapacity = table.Column<int>(type: "integer", nullable: true),
                    DefaultPlatformFeePercent = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rule_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultPriceCents = table.Column<int>(type: "integer", nullable: false),
                    DefaultFeePercent = table.Column<int>(type: "integer", nullable: true),
                    DefaultFeeFlatCents = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rule_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_type_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DefaultPriceCents = table.Column<int>(type: "integer", nullable: false),
                    DefaultPlatformFeeCents = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_type_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    ZipCode = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    OptInLocationEmail = table.Column<bool>(type: "boolean", nullable: false),
                    HasCompletedOnboarding = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    ImagePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "table_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DefaultCapacity = table.Column<int>(type: "integer", nullable: false),
                    DefaultShape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultPriceCents = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_table_types_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "venue_layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EditorMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GridRows = table.Column<int>(type: "integer", nullable: true),
                    GridCols = table.Column<int>(type: "integer", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_layouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_layouts_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: true),
                    PlatformFeePercent = table.Column<int>(type: "integer", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledPublishAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditorMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GridRows = table.Column<int>(type: "integer", nullable: true),
                    GridCols = table.Column<int>(type: "integer", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "Description" }),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    VenueLayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_events_event_templates_EventTemplateId",
                        column: x => x.EventTemplateId,
                        principalTable: "event_templates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_events_users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_events_venue_layouts_VenueLayoutId",
                        column: x => x.VenueLayoutId,
                        principalTable: "venue_layouts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_events_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_layout_tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GridRow = table.Column<int>(type: "integer", nullable: true),
                    GridCol = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VenueLayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_layout_tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_layout_tables_table_types_TableTypeId",
                        column: x => x.TableTypeId,
                        principalTable: "table_types",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_venue_layout_tables_venue_layouts_VenueLayoutId",
                        column: x => x.VenueLayoutId,
                        principalTable: "venue_layouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtotalCents = table.Column<int>(type: "integer", nullable: false),
                    FeeCents = table.Column<int>(type: "integer", nullable: false),
                    TotalCents = table.Column<int>(type: "integer", nullable: false),
                    QrToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bookings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxCount = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    FeePercent = table.Column<int>(type: "integer", nullable: true),
                    FeeFlatCents = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.Id);
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
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_pricing_rules_table_types_TableTypeId",
                        column: x => x.TableTypeId,
                        principalTable: "table_types",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: true),
                    QuantityTotal = table.Column<int>(type: "integer", nullable: false),
                    QuantitySold = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PlatformFeeCents = table.Column<int>(type: "integer", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_types", x => x.Id);
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
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    Shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    PriceOverrideCents = table.Column<int>(type: "integer", nullable: true),
                    PlatformFeeCents = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GridRow = table.Column<int>(type: "integer", nullable: true),
                    GridCol = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TableTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueLayoutTableId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tables_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tables_table_types_TableTypeId",
                        column: x => x.TableTypeId,
                        principalTable: "table_types",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tables_venue_layout_tables_VenueLayoutTableId",
                        column: x => x.VenueLayoutTableId,
                        principalTable: "venue_layout_tables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tables_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RefundId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seats_tables_TableId",
                        column: x => x.TableId,
                        principalTable: "tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    QrToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GuestName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GuestEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InvitationToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InvitationSent = table.Column<bool>(type: "boolean", nullable: false),
                    IsCheckedIn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_items", x => x.Id);
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
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_booking_items_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seat_holds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_holds", x => x.Id);
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seat_holds_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_logs_Action",
                table: "admin_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_admin_logs_Timestamp",
                table: "admin_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_app_settings_Key",
                table: "app_settings",
                column: "Key",
                unique: true);

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
                name: "IX_bookings_BookingNumber",
                table: "bookings",
                column: "BookingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_EventId",
                table: "bookings",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_QrToken",
                table: "bookings",
                column: "QrToken",
                unique: true,
                filter: "\"QrToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_Status",
                table: "bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId",
                table: "bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_developer_logs_Severity",
                table: "developer_logs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_developer_logs_Timestamp",
                table: "developer_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_Timestamp",
                table: "email_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_events_Category",
                table: "events",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_events_EventTemplateId",
                table: "events",
                column: "EventTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_events_OrganizerId",
                table: "events",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_events_SearchVector",
                table: "events",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_events_Slug",
                table: "events",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_StartDate",
                table: "events",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_events_Status",
                table: "events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_events_VenueId",
                table: "events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_events_VenueLayoutId",
                table: "events",
                column: "VenueLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_Email",
                table: "magic_link_tokens",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_ExpiresAt",
                table: "magic_link_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_TokenHash",
                table: "magic_link_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BookingId",
                table: "payments",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_PaymentIntentId",
                table: "payments",
                column: "PaymentIntentId",
                unique: true);

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
                name: "IX_seat_holds_SeatId_EventId_IsActive",
                table: "seat_holds",
                columns: new[] { "SeatId", "EventId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_TicketTypeId",
                table: "seat_holds",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_UserId",
                table: "seat_holds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_seats_TableId",
                table: "seats",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_Category",
                table: "system_logs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_Timestamp",
                table: "system_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_table_types_VenueId",
                table: "table_types",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId",
                table: "tables",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_TableTypeId",
                table: "tables",
                column: "TableTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_VenueId",
                table: "tables",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_VenueLayoutTableId",
                table: "tables",
                column: "VenueLayoutTableId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_EventId",
                table: "ticket_types",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_TemplateId",
                table: "ticket_types",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailHash",
                table: "users",
                column: "EmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_layout_tables_TableTypeId",
                table: "venue_layout_tables",
                column: "TableTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layout_tables_VenueLayoutId",
                table: "venue_layout_tables",
                column: "VenueLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_layouts_VenueId",
                table: "venue_layouts",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_venues_City",
                table: "venues",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_venues_Name",
                table: "venues",
                column: "Name");

            // ─── SQL Views ───────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE VIEW v_events AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Description"", e.""Status"",
    COALESCE(e.""Category"", et.""Category"", 'Music') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    COALESCE(e.""LayoutMode"", vl.""LayoutMode"", 'None') AS ""LayoutMode"",
    COALESCE(e.""EditorMode"", vl.""EditorMode"") AS ""EditorMode"",
    COALESCE(e.""GridRows"", vl.""GridRows"") AS ""GridRows"",
    COALESCE(e.""GridCols"", vl.""GridCols"") AS ""GridCols"",
    COALESCE(e.""MaxCapacity"", et.""DefaultMaxCapacity"") AS ""MaxCapacity"",
    COALESCE(e.""PlatformFeePercent"", et.""DefaultPlatformFeePercent"") AS ""PlatformFeePercent"",
    e.""PublishedAt"", e.""ScheduledPublishAt"",
    e.""VenueId"", e.""OrganizerId"", e.""VenueLayoutId"", e.""EventTemplateId"",
    e.""SearchVector"", e.""CreatedAt"", e.""UpdatedAt"",
    v.""Name"" AS ""VenueName"", v.""Address"" AS ""VenueAddress"",
    v.""City"" AS ""VenueCity"", v.""State"" AS ""VenueState"",
    v.""ZipCode"" AS ""VenueZipCode"", v.""ImagePath"" AS ""VenueImagePath""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN venue_layouts vl ON e.""VenueLayoutId"" = vl.""Id""
LEFT JOIN event_templates et ON e.""EventTemplateId"" = et.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_ticket_types AS
SELECT
    tt.""Id"", tt.""EventId"",
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
    t.""Section"",
    t.""PriceType"",
    COALESCE(t.""PriceOverrideCents"", t.""PriceCents"", ttype.""DefaultPriceCents"", 0) AS ""EffectivePriceCents"",
    t.""PlatformFeeCents"", t.""IsActive"",
    t.""GridRow"", t.""GridCol"", t.""SortOrder"",
    t.""CreatedAt"", t.""UpdatedAt""
FROM tables t
LEFT JOIN table_types ttype ON t.""TableTypeId"" = ttype.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_pricing_rules AS
SELECT
    pr.""Id"", pr.""EventId"", pr.""TableTypeId"",
    COALESCE(pr.""Name"", prt.""Name"") AS ""Name"",
    COALESCE(pr.""Type"", prt.""Type"", 'Standard') AS ""Type"",
    COALESCE(pr.""PriceCents"", prt.""DefaultPriceCents"", 0) AS ""PriceCents"",
    pr.""ValidFrom"", pr.""ValidUntil"", pr.""MaxCount"", pr.""UsedCount"",
    pr.""IsActive"", pr.""SortOrder"",
    COALESCE(pr.""FeePercent"", prt.""DefaultFeePercent"") AS ""FeePercent"",
    COALESCE(pr.""FeeFlatCents"", prt.""DefaultFeeFlatCents"") AS ""FeeFlatCents"",
    COALESCE(pr.""Description"", prt.""Description"") AS ""Description"",
    pr.""TemplateId"", pr.""CreatedAt"", pr.""UpdatedAt""
FROM pricing_rules pr
LEFT JOIN pricing_rule_templates prt ON pr.""TemplateId"" = prt.""Id"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_event_summary AS
SELECT
    e.""Id"", e.""Title"", e.""Slug"", e.""Status"",
    COALESCE(e.""Category"", 'Music') AS ""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    v.""Name"" AS ""VenueName"", v.""City"" AS ""VenueCity"",
    u.""Name"" AS ""OrganizerName"",
    COUNT(DISTINCT tt.""Id"")::int AS ""TicketTypeCount"",
    COALESCE(SUM(tt.""QuantityTotal""), 0) AS ""TotalCapacity"",
    COALESCE(SUM(tt.""QuantitySold""), 0) AS ""TotalSold""
FROM events e
JOIN venues v ON e.""VenueId"" = v.""Id""
JOIN users u ON e.""OrganizerId"" = u.""Id""
LEFT JOIN ticket_types tt ON tt.""EventId"" = e.""Id""
GROUP BY e.""Id"", e.""Title"", e.""Slug"", e.""Status"", e.""Category"",
    e.""StartDate"", e.""EndDate"", e.""ImagePath"", e.""IsFeatured"",
    v.""Name"", v.""City"", u.""Name"";
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_summary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_pricing_rules;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_tables;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_ticket_types;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events;");

            migrationBuilder.DropTable(
                name: "admin_logs");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "booking_items");

            migrationBuilder.DropTable(
                name: "developer_logs");

            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "magic_link_tokens");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "seat_holds");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "pricing_rule_templates");

            migrationBuilder.DropTable(
                name: "seats");

            migrationBuilder.DropTable(
                name: "ticket_types");

            migrationBuilder.DropTable(
                name: "tables");

            migrationBuilder.DropTable(
                name: "ticket_type_templates");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "venue_layout_tables");

            migrationBuilder.DropTable(
                name: "event_templates");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "table_types");

            migrationBuilder.DropTable(
                name: "venue_layouts");

            migrationBuilder.DropTable(
                name: "venues");
        }
    }
}
