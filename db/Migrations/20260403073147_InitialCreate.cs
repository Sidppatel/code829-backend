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
                name: "addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Line1 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Line2 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EncryptedValue = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "developer_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.CheckConstraint("CK_developer_logs_Severity", "\"Severity\" IN ('Warning','Error','Critical')");
                });

            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                name: "magic_link_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_tokens", x => x.Id);
                    table.CheckConstraint("CK_magic_link_tokens_Usage", "(\"IsUsed\" = false AND \"UsedAt\" IS NULL) OR (\"IsUsed\" = true AND \"UsedAt\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.CheckConstraint("CK_system_logs_Category", "\"Category\" IN ('EntityChange','BackgroundWorker','Cache','MockService','Migration')");
                    table.CheckConstraint("CK_system_logs_DurationMs", "\"DurationMs\" IS NULL OR \"DurationMs\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "table_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DefaultCapacity = table.Column<int>(type: "integer", nullable: false),
                    DefaultShape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultPriceCents = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_types", x => x.Id);
                    table.CheckConstraint("CK_table_types_DefaultCapacity", "\"DefaultCapacity\" > 0");
                    table.CheckConstraint("CK_table_types_DefaultPriceCents", "\"DefaultPriceCents\" >= 0");
                    table.CheckConstraint("CK_table_types_DefaultShape", "\"DefaultShape\" IN ('Round','Rectangle','Square','Cocktail')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    OptInLocationEmail = table.Column<bool>(type: "boolean", nullable: false),
                    HasCompletedOnboarding = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.CheckConstraint("CK_users_Role", "\"Role\" IN ('User','Staff','Admin','Developer')");
                    table.ForeignKey(
                        name: "FK_users_addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    ImagePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venues_addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: true),
                    PricePerPersonCents = table.Column<int>(type: "integer", nullable: true),
                    PlatformFeePercent = table.Column<int>(type: "integer", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledPublishAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GridRows = table.Column<int>(type: "integer", nullable: true),
                    GridCols = table.Column<int>(type: "integer", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "Description" }),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                    table.CheckConstraint("CK_events_Category", "\"Category\" IS NULL OR \"Category\" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
                    table.CheckConstraint("CK_events_CompletedRequiresPublish", "\"Status\" <> 'Completed' OR \"PublishedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_events_DateRange", "\"EndDate\" > \"StartDate\"");
                    table.CheckConstraint("CK_events_DraftNoPublishDate", "\"Status\" <> 'Draft' OR \"PublishedAt\" IS NULL");
                    table.CheckConstraint("CK_events_GridDimensions", "(\"GridRows\" IS NULL OR \"GridRows\" > 0) AND (\"GridCols\" IS NULL OR \"GridCols\" > 0)");
                    table.CheckConstraint("CK_events_LayoutMode", "\"LayoutMode\" IN ('Grid','Open')");
                    table.CheckConstraint("CK_events_MaxCapacity", "\"MaxCapacity\" IS NULL OR \"MaxCapacity\" > 0");
                    table.CheckConstraint("CK_events_PlatformFeePercent", "\"PlatformFeePercent\" IS NULL OR (\"PlatformFeePercent\" >= 0 AND \"PlatformFeePercent\" <= 100)");
                    table.CheckConstraint("CK_events_PricePerPersonCents", "\"PricePerPersonCents\" IS NULL OR \"PricePerPersonCents\" >= 0");
                    table.CheckConstraint("CK_events_PublishLifecycle", "\"Status\" <> 'Published' OR \"PublishedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_events_Status", "\"Status\" IN ('Draft','Published','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_events_users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_events_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Available"),
                    LockedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PosX = table.Column<double>(type: "double precision", nullable: false),
                    PosY = table.Column<double>(type: "double precision", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TableTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tables", x => x.Id);
                    table.CheckConstraint("CK_tables_AvailableNoLock", "\"Status\" <> 'Available' OR (\"LockedByUserId\" IS NULL AND \"LockExpiresAt\" IS NULL)");
                    table.CheckConstraint("CK_tables_Capacity", "\"Capacity\" > 0");
                    table.CheckConstraint("CK_tables_LockedRequiresOwner", "\"Status\" <> 'Locked' OR (\"LockedByUserId\" IS NOT NULL AND \"LockExpiresAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_tables_PriceCents", "\"PriceCents\" >= 0");
                    table.CheckConstraint("CK_tables_Shape", "\"Shape\" IN ('Round','Rectangle','Square','Cocktail')");
                    table.CheckConstraint("CK_tables_Status", "\"Status\" IN ('Available','Locked','Booked')");
                    table.ForeignKey(
                        name: "FK_tables_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tables_table_types_TableTypeId",
                        column: x => x.TableTypeId,
                        principalTable: "table_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tables_users_LockedByUserId",
                        column: x => x.LockedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tables_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BookingNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtotalCents = table.Column<int>(type: "integer", nullable: false),
                    FeeCents = table.Column<int>(type: "integer", nullable: false),
                    TotalCents = table.Column<int>(type: "integer", nullable: false),
                    QrToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TableId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeatsReserved = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.CheckConstraint("CK_bookings_FeeCents", "\"FeeCents\" >= 0");
                    table.CheckConstraint("CK_bookings_SeatsReserved", "\"SeatsReserved\" IS NULL OR \"SeatsReserved\" > 0");
                    table.CheckConstraint("CK_bookings_Status", "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                    table.CheckConstraint("CK_bookings_SubtotalCents", "\"SubtotalCents\" >= 0");
                    table.CheckConstraint("CK_bookings_TotalCents", "\"TotalCents\" >= 0");
                    table.CheckConstraint("CK_bookings_TotalFormula", "\"TotalCents\" = \"SubtotalCents\" + \"FeeCents\"");
                    table.ForeignKey(
                        name: "FK_bookings_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_tables_TableId",
                        column: x => x.TableId,
                        principalTable: "tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bookings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RefundId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.CheckConstraint("CK_payments_AmountCents", "\"AmountCents\" >= 0");
                    table.CheckConstraint("CK_payments_Currency", "\"Currency\" IN ('usd')");
                    table.CheckConstraint("CK_payments_NotRefundedNoRefundDate", "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");
                    table.CheckConstraint("CK_payments_PaidLifecycle", "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");
                    table.CheckConstraint("CK_payments_PendingNoPaidDate", "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");
                    table.CheckConstraint("CK_payments_RefundLifecycle", "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_payments_Status", "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                    table.ForeignKey(
                        name: "FK_payments_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_bookings_BookingNumber",
                table: "bookings",
                column: "BookingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_EventId_Status",
                table: "bookings",
                columns: new[] { "EventId", "Status" });

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
                name: "IX_bookings_TableId",
                table: "bookings",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId",
                table: "bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId_CreatedAt",
                table: "bookings",
                columns: new[] { "UserId", "CreatedAt" });

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
                name: "IX_events_Status_StartDate",
                table: "events",
                columns: new[] { "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_events_VenueId",
                table: "events",
                column: "VenueId");

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
                name: "IX_payments_Status_PaidAt",
                table: "payments",
                columns: new[] { "Status", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_Category",
                table: "system_logs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_Timestamp",
                table: "system_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId",
                table: "tables",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId_Label",
                table: "tables",
                columns: new[] { "EventId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId_Status",
                table: "tables",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tables_LockedByUserId",
                table: "tables",
                column: "LockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_TableTypeId",
                table: "tables",
                column: "TableTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_VenueId",
                table: "tables",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_users_AddressId",
                table: "users",
                column: "AddressId");

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
                name: "IX_venues_AddressId",
                table: "venues",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_venues_Name",
                table: "venues",
                column: "Name");

            // ─── Create read-only views ─────────────────────────────────

            migrationBuilder.Sql(@"
CREATE VIEW v_events AS
SELECT
    e.""Id"",
    e.""Title"",
    e.""Slug"",
    e.""Description"",
    e.""Status"",
    e.""Category"",
    e.""StartDate"",
    e.""EndDate"",
    e.""ImagePath"",
    e.""IsFeatured"",
    e.""LayoutMode"",
    e.""MaxCapacity"",
    e.""PricePerPersonCents"",
    e.""PlatformFeePercent"",
    e.""GridRows"",
    e.""GridCols"",
    e.""PublishedAt"",
    e.""ScheduledPublishAt"",
    e.""VenueId"",
    e.""OrganizerId"",
    e.""SearchVector"",
    e.""CreatedAt"",
    e.""UpdatedAt"",
    v.""Name""       AS ""VenueName"",
    COALESCE(a.""Line1"", '')  AS ""VenueAddress"",
    COALESCE(a.""City"", '')   AS ""VenueCity"",
    COALESCE(a.""State"", '')  AS ""VenueState"",
    COALESCE(a.""ZipCode"", '') AS ""VenueZipCode"",
    v.""ImagePath""  AS ""VenueImagePath""
FROM events e
JOIN venues v ON v.""Id"" = e.""VenueId""
LEFT JOIN addresses a ON a.""Id"" = v.""AddressId"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_event_summary AS
SELECT
    e.""Id"",
    e.""Title"",
    e.""Slug"",
    e.""Status"",
    e.""Category"",
    e.""StartDate"",
    e.""EndDate"",
    e.""ImagePath"",
    e.""IsFeatured"",
    e.""LayoutMode"",
    v.""Name""  AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    CONCAT(u.""FirstName"", ' ', u.""LastName"") AS ""OrganizerName"",
    CASE
        WHEN e.""LayoutMode"" = 'Grid' THEN COALESCE((SELECT SUM(t.""Capacity"") FROM tables t WHERE t.""EventId"" = e.""Id"" AND t.""IsActive""), 0)
        ELSE COALESCE(e.""MaxCapacity"", 0)
    END AS ""TotalCapacity"",
    COALESCE((SELECT COALESCE(SUM(b.""SeatsReserved""), 0) + COUNT(CASE WHEN b.""TableId"" IS NOT NULL AND b.""SeatsReserved"" IS NULL THEN 1 END)
              FROM bookings b WHERE b.""EventId"" = e.""Id"" AND b.""Status"" IN ('Paid', 'CheckedIn')), 0) AS ""TotalSold""
FROM events e
JOIN venues v ON v.""Id"" = e.""VenueId""
LEFT JOIN addresses a ON a.""Id"" = v.""AddressId""
JOIN users u ON u.""Id"" = e.""OrganizerId"";
");

            migrationBuilder.Sql(@"
CREATE VIEW v_tables AS
SELECT
    t.""Id"",
    t.""EventId"",
    t.""VenueId"",
    t.""TableTypeId"",
    t.""Label"",
    t.""Capacity"",
    t.""Shape"",
    t.""Color"",
    t.""PriceCents"",
    t.""IsActive"",
    t.""PosX"",
    t.""PosY"",
    t.""SortOrder"",
    t.""CreatedAt"",
    t.""UpdatedAt""
FROM tables t;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_events CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_event_summary CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_tables CASCADE;");

            migrationBuilder.DropTable(
                name: "admin_logs");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "developer_logs");

            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "magic_link_tokens");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "tables");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "table_types");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropTable(
                name: "addresses");
        }
    }
}
