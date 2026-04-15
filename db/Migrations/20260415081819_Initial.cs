using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

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
                name: "admin_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvatarPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StripeConnectedAccountId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                    table.CheckConstraint("CK_admin_users_Role", "\"Role\" IN ('Staff','Admin','Developer')");
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
                name: "table_templates",
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
                    table.PrimaryKey("PK_table_templates", x => x.Id);
                    table.CheckConstraint("CK_table_templates_DefaultCapacity", "\"DefaultCapacity\" > 0");
                    table.CheckConstraint("CK_table_templates_DefaultPriceCents", "\"DefaultPriceCents\" >= 0");
                    table.CheckConstraint("CK_table_templates_DefaultShape", "\"DefaultShape\" IN ('Round','Rectangle','Square','Cocktail')");
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
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    OptInLocationEmail = table.Column<bool>(type: "boolean", nullable: false),
                    HasCompletedOnboarding = table.Column<bool>(type: "boolean", nullable: false),
                    StripeConnectedAccountId = table.Column<string>(type: "text", nullable: true),
                    AvatarPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
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
                name: "invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvitedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                    table.CheckConstraint("CK_invitations_Role", "\"Role\" IN ('Staff','Admin','Developer')");
                    table.CheckConstraint("CK_invitations_Status", "\"Status\" IN ('Pending','Accepted','Revoked','Expired')");
                    table.ForeignKey(
                        name: "FK_invitations_admin_users_InvitedByAdminUserId",
                        column: x => x.InvitedByAdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeviceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_sessions", x => x.Id);
                    table.CheckConstraint("CK_device_sessions_UserType", "(\"UserId\" IS NOT NULL AND \"AdminUserId\" IS NULL) OR (\"UserId\" IS NULL AND \"AdminUserId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_device_sessions_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feedbacks_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EntityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SizeBytes = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_images_users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    table.CheckConstraint("CK_events_PublishLifecycle", "\"Status\" <> 'Published' OR \"PublishedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_events_Status", "\"Status\" IN ('Draft','Published','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_events_admin_users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "admin_users",
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
                name: "event_tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    PlatformFeeCents = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_tables", x => x.Id);
                    table.CheckConstraint("CK_event_tables_Capacity", "\"Capacity\" > 0");
                    table.CheckConstraint("CK_event_tables_PriceCents", "\"PriceCents\" >= 0");
                    table.CheckConstraint("CK_event_tables_Shape", "\"Shape\" IN ('Round','Rectangle','Square','Cocktail')");
                    table.ForeignKey(
                        name: "FK_event_tables_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tables_table_templates_TableTemplateId",
                        column: x => x.TableTemplateId,
                        principalTable: "table_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                    Description = table.Column<string>(type: "text", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GridRow = table.Column<int>(type: "integer", nullable: false),
                    GridCol = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Available"),
                    LockedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tables", x => x.Id);
                    table.CheckConstraint("CK_tables_AvailableNoLock", "\"Status\" <> 'Available' OR (\"LockedByUserId\" IS NULL AND \"LockExpiresAt\" IS NULL)");
                    table.CheckConstraint("CK_tables_GridCol", "\"GridCol\" >= 0");
                    table.CheckConstraint("CK_tables_GridRow", "\"GridRow\" >= 0");
                    table.CheckConstraint("CK_tables_LockedRequiresOwner", "\"Status\" <> 'Locked' OR (\"LockedByUserId\" IS NOT NULL AND \"LockExpiresAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_tables_Status", "\"Status\" IN ('Available','Locked','Booked')");
                    table.ForeignKey(
                        name: "FK_tables_event_tables_EventTableId",
                        column: x => x.EventTableId,
                        principalTable: "event_tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tables_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tables_users_LockedByUserId",
                        column: x => x.LockedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    EventTicketTypeId = table.Column<Guid>(type: "uuid", nullable: true),
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
                        name: "FK_bookings_event_ticket_types_EventTicketTypeId",
                        column: x => x.EventTicketTypeId,
                        principalTable: "event_ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "booking_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TicketCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QrToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InviteTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InviteExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InviteSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_tickets", x => x.Id);
                    table.CheckConstraint("CK_booking_tickets_SeatNumber", "\"SeatNumber\" > 0");
                    table.CheckConstraint("CK_booking_tickets_Status", "\"Status\" IN ('Unassigned','Invited','Claimed','CheckedIn')");
                    table.ForeignKey(
                        name: "FK_booking_tickets_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_tickets_users_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "IX_admin_users_Email",
                table: "admin_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_EmailHash",
                table: "admin_users",
                column: "EmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_settings_Key",
                table: "app_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_tickets_BookingId_SeatNumber",
                table: "booking_tickets",
                columns: new[] { "BookingId", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_tickets_GuestUserId",
                table: "booking_tickets",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_tickets_InviteTokenHash",
                table: "booking_tickets",
                column: "InviteTokenHash",
                unique: true,
                filter: "\"InviteTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_booking_tickets_QrToken",
                table: "booking_tickets",
                column: "QrToken",
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
                name: "IX_bookings_EventTicketTypeId",
                table: "bookings",
                column: "EventTicketTypeId");

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
                name: "IX_device_sessions_Active",
                table: "device_sessions",
                columns: new[] { "ExpiresAt", "RevokedAt" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_AdminUserId",
                table: "device_sessions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_SessionHash",
                table: "device_sessions",
                column: "SessionHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_UserId",
                table: "device_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_Timestamp",
                table: "email_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_event_tables_EventId_Label",
                table: "event_tables",
                columns: new[] { "EventId", "Label" });

            migrationBuilder.CreateIndex(
                name: "IX_event_tables_TableTemplateId",
                table: "event_tables",
                column: "TableTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_event_ticket_types_EventId_Label",
                table: "event_ticket_types",
                columns: new[] { "EventId", "Label" });

            migrationBuilder.CreateIndex(
                name: "IX_event_ticket_types_EventId_SortOrder",
                table: "event_ticket_types",
                columns: new[] { "EventId", "SortOrder" });

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
                name: "IX_feedbacks_CreatedAt",
                table: "feedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_feedbacks_Type",
                table: "feedbacks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_feedbacks_UserId",
                table: "feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_images_EntityType_EntityId",
                table: "images",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_images_UploadedById",
                table: "images",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_Email",
                table: "invitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_InvitedByAdminUserId",
                table: "invitations",
                column: "InvitedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_TokenHash",
                table: "invitations",
                column: "TokenHash",
                unique: true);

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
                name: "IX_tables_EventId_GridRow_GridCol",
                table: "tables",
                columns: new[] { "EventId", "GridRow", "GridCol" },
                unique: true);

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
                name: "IX_tables_EventTableId",
                table: "tables",
                column: "EventTableId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_LockedByUserId",
                table: "tables",
                column: "LockedByUserId");

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

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_events AS
SELECT
    e.""Id"" AS ""Id"",
    e.""Title"" AS ""Title"",
    e.""Slug"" AS ""Slug"",
    e.""Description"" AS ""Description"",
    e.""Status""::text AS ""Status"",
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"" AS ""StartDate"",
    e.""EndDate"" AS ""EndDate"",
    e.""ImagePath"" AS ""ImagePath"",
    e.""IsFeatured"" AS ""IsFeatured"",
    e.""LayoutMode""::text AS ""LayoutMode"",
    e.""MaxCapacity"" AS ""MaxCapacity"",
    ettp.min_price::int AS ""PricePerPersonCents"",
    e.""GridRows"" AS ""GridRows"",
    e.""GridCols"" AS ""GridCols"",
    e.""PublishedAt"" AS ""PublishedAt"",
    e.""ScheduledPublishAt"" AS ""ScheduledPublishAt"",
    e.""VenueId"" AS ""VenueId"",
    e.""OrganizerId"" AS ""OrganizerId"",
    e.""CreatedAt"" AS ""CreatedAt"",
    e.""UpdatedAt"" AS ""UpdatedAt"",
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
    COALESCE(
        e.""MaxCapacity"",
        CASE
            WHEN e.""LayoutMode""::text = 'Grid' THEN table_cap.total_seats
            ELSE ett_cap.total_qty
        END,
        0
    )::int AS ""TotalCapacity"",
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
) ett_cap ON true
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(et.""Capacity""), 0)::int AS total_seats
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true
) table_cap ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_event_summary AS
SELECT
    e.""Id"" AS ""Id"",
    e.""Title"" AS ""Title"",
    e.""Slug"" AS ""Slug"",
    e.""Status""::text AS ""Status"",
    COALESCE(e.""Category""::text, '') AS ""Category"",
    e.""StartDate"" AS ""StartDate"",
    e.""EndDate"" AS ""EndDate"",
    e.""ImagePath"" AS ""ImagePath"",
    img.""StorageKey"" AS ""PrimaryImageKey"",
    e.""IsFeatured"" AS ""IsFeatured"",
    e.""LayoutMode""::text AS ""LayoutMode"",
    ettp.min_price::int AS ""PricePerPersonCents"",
    e.""MaxCapacity"" AS ""MaxCapacity"",
    e.""VenueId"" AS ""VenueId"",
    v.""Name"" AS ""VenueName"",
    COALESCE(a.""City"", '') AS ""VenueCity"",
    COALESCE(a.""State"", '') AS ""VenueState"",
    e.""OrganizerId"" AS ""OrganizerId"",
    COALESCE(au.""FirstName"" || ' ' || au.""LastName"", '') AS ""OrganizerName"",
    COALESCE(
        e.""MaxCapacity"",
        CASE
            WHEN e.""LayoutMode""::text = 'Grid' THEN table_cap.total_seats
            ELSE ett_cap.total_qty
        END,
        0
    )::int AS ""TotalCapacity"",
    COALESCE(bs.sold, 0)::int AS ""TotalSold"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    ts.min_price::int AS ""MinTablePriceCents"",
    ettp.min_price::int AS ""MinTicketTypePriceCents"",
    e.""CreatedAt"" AS ""CreatedAt""
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
) ett_cap ON true
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(et.""Capacity""), 0)::int AS total_seats
    FROM tables t
    JOIN event_tables et ON t.""EventTableId"" = et.""Id""
    WHERE t.""EventId"" = e.""Id"" AND t.""IsActive"" = true
) table_cap ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_user_profile AS
SELECT
    u.""Id"", u.""Email"", u.""FirstName"", u.""LastName"",
    u.""IsActive"", u.""LastLoginAt"",
    u.""Phone"", u.""OptInLocationEmail"", u.""HasCompletedOnboarding"",
    u.""AvatarPath"", u.""CreatedAt"",
    a.""Line1"" AS ""AddressLine1"",
    a.""City"", a.""State"", a.""ZipCode""
FROM users u
LEFT JOIN addresses a ON u.""AddressId"" = a.""Id"";
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_tables AS
SELECT
    t.""Id"", t.""EventId"", t.""EventTableId"",
    t.""Label"", t.""GridRow"", t.""GridCol"",
    t.""IsActive"", t.""SortOrder"",
    t.""Status""::text,
    t.""LockedByUserId"", t.""LockExpiresAt"",
    t.""CreatedAt"", t.""UpdatedAt"",
    et.""Capacity"", et.""Shape""::text, et.""Color"",
    et.""PriceCents"", et.""PlatformFeeCents"",
    et.""Label"" AS ""EventTableLabel""
FROM tables t
JOIN event_tables et ON t.""EventTableId"" = et.""Id"";
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_bookings AS
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

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_booking_tickets AS
SELECT
    bt.""Id"", bt.""TicketCode"", bt.""QrToken"", bt.""SeatNumber"",
    bt.""Status""::text,
    bt.""CreatedAt"",
    bt.""InvitedEmail"", bt.""InviteSentAt"", bt.""InviteExpiresAt"", bt.""ClaimedAt"",
    bt.""BookingId"",
    b.""BookingNumber"", b.""Status""::text AS ""BookingStatus"",
    bt.""GuestUserId"",
    gu.""Email"" AS ""GuestEmail"",
    gu.""FirstName"" AS ""GuestFirstName"",
    gu.""LastName"" AS ""GuestLastName"",
    e.""Id"" AS ""EventId"",
    e.""Title"" AS ""EventTitle"",
    e.""StartDate"" AS ""EventStartDate"",
    e.""EndDate"" AS ""EventEndDate"",
    v.""Name"" AS ""VenueName"",
    COALESCE(addr.""City"", '') AS ""VenueCity"",
    b.""UserId"" AS ""BookingUserId"",
    bu.""Email"" AS ""BookingUserEmail""
FROM booking_tickets bt
JOIN bookings b ON bt.""BookingId"" = b.""Id""
JOIN events e ON b.""EventId"" = e.""Id""
JOIN venues v ON e.""VenueId"" = v.""Id""
LEFT JOIN addresses addr ON v.""AddressId"" = addr.""Id""
LEFT JOIN users gu ON bt.""GuestUserId"" = gu.""Id""
JOIN users bu ON b.""UserId"" = bu.""Id"";
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_venues AS
SELECT
    v.""Id"", v.""Name"", v.""Description"", v.""ImagePath"",
    v.""Phone"", v.""Email"", v.""Website"",
    v.""IsActive"", v.""CreatedAt"",
    COALESCE(a.""Line1"", '') AS ""AddressLine1"",
    a.""Line2"" AS ""AddressLine2"",
    COALESCE(a.""City"", '') AS ""City"",
    COALESCE(a.""State"", '') AS ""State"",
    COALESCE(a.""ZipCode"", '') AS ""ZipCode"",
    COALESCE(ec.cnt, 0)::int AS ""EventCount""
FROM venues v
LEFT JOIN addresses a ON v.""AddressId"" = a.""Id""
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM events e WHERE e.""VenueId"" = v.""Id""
) ec ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_event_tables_summary AS
SELECT
    et.""Id"", et.""EventId"", et.""Label"", et.""Capacity"",
    et.""Shape""::text, et.""Color"", et.""PriceCents"", et.""PlatformFeeCents"",
    et.""IsActive"",
    COALESCE(ts.total, 0)::int AS ""TotalTables"",
    COALESCE(ts.available, 0)::int AS ""AvailableTables"",
    COALESCE(ts.locked, 0)::int AS ""LockedTables"",
    COALESCE(ts.booked, 0)::int AS ""BookedTables""
FROM event_tables et
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS total,
        COUNT(*) FILTER (WHERE t.""Status"" = 'Available' AND t.""IsActive"")::int AS available,
        COUNT(*) FILTER (WHERE t.""Status"" = 'Locked')::int AS locked,
        COUNT(*) FILTER (WHERE t.""Status"" = 'Booked')::int AS booked
    FROM tables t WHERE t.""EventTableId"" = et.""Id""
) ts ON true;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_event_ticket_types_summary AS
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

            // ─── AUTH STORED PROCEDURES ───────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_magic_link(
    p_email text, p_token_hash text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO magic_link_tokens (""Id"", ""TokenHash"", ""Email"", ""ExpiresAt"", ""IsUsed"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_token_hash, p_email, p_expires_at, false, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_consume_magic_link(p_token_hash text)
RETURNS TABLE (
    ""Id"" uuid, ""Email"" text, ""ExpiresAt"" timestamptz
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE magic_link_tokens
    SET ""IsUsed"" = true, ""UsedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""TokenHash"" = p_token_hash AND ""IsUsed"" = false AND ""ExpiresAt"" > now()
    RETURNING magic_link_tokens.""Id"", magic_link_tokens.""Email"", magic_link_tokens.""ExpiresAt"";
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_upsert_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    SELECT ""Id"" INTO v_id FROM users WHERE ""Email"" = p_email;
    IF v_id IS NULL THEN
        INSERT INTO users (""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
            ""IsActive"", ""LastLoginAt"", ""OptInLocationEmail"", ""HasCompletedOnboarding"",
            ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), p_email, p_email_hash, p_first_name, p_last_name,
            true, now(), false, false, now(), now())
        RETURNING ""Id"" INTO v_id;
    ELSE
        UPDATE users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now() WHERE ""Id"" = v_id;
    END IF;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_user_last_login(p_user_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now() WHERE ""Id"" = p_user_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_device_session(
    p_user_id uuid, p_session_hash text, p_fingerprint text,
    p_device_name text, p_ip text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO device_sessions (""Id"", ""UserId"", ""SessionHash"", ""DeviceFingerprint"",
        ""DeviceName"", ""IpAddress"", ""LastActivityAt"", ""ExpiresAt"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_user_id, p_session_hash, p_fingerprint,
        p_device_name, p_ip, now(), p_expires_at, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_revoke_device_session(p_session_hash text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE device_sessions SET ""RevokedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""SessionHash"" = p_session_hash AND ""RevokedAt"" IS NULL;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_revoke_all_user_sessions(
    p_user_id uuid, p_except_hash text DEFAULT NULL
) RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_count int;
BEGIN
    UPDATE device_sessions SET ""RevokedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""UserId"" = p_user_id AND ""RevokedAt"" IS NULL
      AND (p_except_hash IS NULL OR ""SessionHash"" <> p_except_hash);
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_cleanup_expired_sessions() RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_count int;
BEGIN
    DELETE FROM device_sessions
    WHERE ""ExpiresAt"" < now()
       OR (""RevokedAt"" IS NOT NULL AND ""RevokedAt"" < now() - interval '7 days');
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_session_activity(p_session_hash text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE device_sessions SET ""LastActivityAt"" = now() WHERE ""SessionHash"" = p_session_hash;
END; $$;
");

            // ─── USER STORED PROCEDURES ───────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_user_profile(
    p_user_id uuid, p_first_name text, p_last_name text, p_phone text,
    p_address text, p_city text, p_state text, p_zip text, p_opt_in bool
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_address_id uuid;
BEGIN
    SELECT ""AddressId"" INTO v_address_id FROM users WHERE ""Id"" = p_user_id;
    IF v_address_id IS NULL AND (p_address IS NOT NULL OR p_city IS NOT NULL) THEN
        INSERT INTO addresses (""Id"", ""Line1"", ""City"", ""State"", ""ZipCode"", ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), COALESCE(p_address,''), COALESCE(p_city,''),
            COALESCE(p_state,''), COALESCE(p_zip,''), now(), now())
        RETURNING ""Id"" INTO v_address_id;
        UPDATE users SET ""AddressId"" = v_address_id WHERE ""Id"" = p_user_id;
    ELSIF v_address_id IS NOT NULL THEN
        UPDATE addresses SET
            ""Line1"" = COALESCE(p_address, ""Line1""),
            ""City"" = COALESCE(p_city, ""City""),
            ""State"" = COALESCE(p_state, ""State""),
            ""ZipCode"" = COALESCE(p_zip, ""ZipCode""),
            ""UpdatedAt"" = now()
        WHERE ""Id"" = v_address_id;
    END IF;
    UPDATE users SET
        ""FirstName"" = COALESCE(p_first_name, ""FirstName""),
        ""LastName"" = COALESCE(p_last_name, ""LastName""),
        ""Phone"" = p_phone,
        ""OptInLocationEmail"" = COALESCE(p_opt_in, ""OptInLocationEmail""),
        ""HasCompletedOnboarding"" = true,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_user_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_user_avatar(p_user_id uuid, p_avatar_path text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE users SET ""AvatarPath"" = p_avatar_path, ""UpdatedAt"" = now() WHERE ""Id"" = p_user_id;
END; $$;
");

            // ─── EVENT STORED PROCEDURES ──────────────────────────────────────────────────

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
        ""MaxCapacity"", ""GridRows"", ""GridCols"", ""VenueId"", ""OrganizerId"",
        ""ScheduledPublishAt"", ""PublishedAt"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_title, p_slug, p_description, p_status,
        CASE WHEN p_category = '' THEN NULL ELSE p_category END,
        p_start_date, p_end_date, p_image_path, COALESCE(p_is_featured, false), p_layout_mode,
        p_max_capacity, p_grid_rows, p_grid_cols, p_venue_id, p_organizer_id,
        p_scheduled_publish_at,
        CASE WHEN p_status = 'Published' THEN now() ELSE NULL END,
        now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_event(
    p_id uuid, p_title text, p_slug text, p_description text, p_category text,
    p_start_date timestamptz, p_end_date timestamptz, p_image_path text, p_is_featured bool,
    p_layout_mode text, p_max_capacity int, p_price_per_person_cents int,
    p_platform_fee_percent int, p_platform_fee_cents int,
    p_grid_rows int, p_grid_cols int, p_venue_id uuid,
    p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE events SET
        ""Title"" = COALESCE(p_title, ""Title""),
        ""Slug"" = COALESCE(p_slug, ""Slug""),
        ""Description"" = COALESCE(p_description, ""Description""),
        ""Category"" = CASE WHEN p_category IS NULL THEN ""Category""
                           WHEN p_category = '' THEN NULL
                           ELSE p_category END,
        ""StartDate"" = COALESCE(p_start_date, ""StartDate""),
        ""EndDate"" = COALESCE(p_end_date, ""EndDate""),
        ""ImagePath"" = COALESCE(p_image_path, ""ImagePath""),
        ""IsFeatured"" = COALESCE(p_is_featured, ""IsFeatured""),
        ""LayoutMode"" = COALESCE(p_layout_mode, ""LayoutMode""),
        ""MaxCapacity"" = p_max_capacity,
        ""GridRows"" = p_grid_rows,
        ""GridCols"" = p_grid_cols,
        ""VenueId"" = COALESCE(p_venue_id, ""VenueId""),
        ""ScheduledPublishAt"" = p_scheduled_publish_at,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_change_event_status(
    p_id uuid, p_status text, p_scheduled_publish_at timestamptz DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE events SET
        ""Status"" = p_status,
        ""PublishedAt"" = CASE WHEN p_status = 'Published' AND ""PublishedAt"" IS NULL THEN now() ELSE ""PublishedAt"" END,
        ""ScheduledPublishAt"" = p_scheduled_publish_at,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_publish_scheduled_events() RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_count int;
BEGIN
    UPDATE events SET
        ""Status"" = 'Published', ""PublishedAt"" = now(),
        ""ScheduledPublishAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Status"" = 'Draft'
      AND ""ScheduledPublishAt"" IS NOT NULL
      AND ""ScheduledPublishAt"" <= now();
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
");

            // ─── EVENT TICKET TYPE STORED PROCEDURES ──────────────────────────────────────

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

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_event_ticket_type(p_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE event_ticket_types SET ""IsActive"" = false, ""UpdatedAt"" = now() WHERE ""Id"" = p_id;
END; $$;
");

            // ─── VENUE STORED PROCEDURES ──────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_venue(
    p_name text, p_description text, p_image_path text,
    p_phone text, p_email text, p_website text,
    p_line1 text, p_line2 text, p_city text, p_state text, p_zip text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid; v_addr_id uuid;
BEGIN
    INSERT INTO addresses (""Id"", ""Line1"", ""Line2"", ""City"", ""State"", ""ZipCode"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), COALESCE(p_line1,''), p_line2, COALESCE(p_city,''),
        COALESCE(p_state,''), COALESCE(p_zip,''), now(), now())
    RETURNING ""Id"" INTO v_addr_id;
    INSERT INTO venues (""Id"", ""Name"", ""Description"", ""ImagePath"", ""Phone"", ""Email"",
        ""Website"", ""IsActive"", ""AddressId"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_name, p_description, p_image_path, p_phone, p_email,
        p_website, true, v_addr_id, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_venue(
    p_id uuid, p_name text, p_description text, p_image_path text,
    p_phone text, p_email text, p_website text, p_is_active bool,
    p_line1 text, p_city text, p_state text, p_zip text
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_addr_id uuid;
BEGIN
    SELECT ""AddressId"" INTO v_addr_id FROM venues WHERE ""Id"" = p_id;
    IF v_addr_id IS NOT NULL THEN
        UPDATE addresses SET
            ""Line1"" = COALESCE(p_line1, ""Line1""),
            ""City"" = COALESCE(p_city, ""City""),
            ""State"" = COALESCE(p_state, ""State""),
            ""ZipCode"" = COALESCE(p_zip, ""ZipCode""),
            ""UpdatedAt"" = now()
        WHERE ""Id"" = v_addr_id;
    END IF;
    UPDATE venues SET
        ""Name"" = COALESCE(p_name, ""Name""),
        ""Description"" = COALESCE(p_description, ""Description""),
        ""ImagePath"" = COALESCE(p_image_path, ""ImagePath""),
        ""Phone"" = p_phone, ""Email"" = p_email, ""Website"" = p_website,
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            // ─── TABLE/LAYOUT STORED PROCEDURES ──────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_event_table(
    p_event_id uuid, p_label text, p_capacity int, p_shape text, p_color text,
    p_price_cents int, p_platform_fee_cents int, p_template_id uuid
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO event_tables (""Id"", ""EventId"", ""Label"", ""Capacity"", ""Shape"", ""Color"",
        ""PriceCents"", ""PlatformFeeCents"", ""IsActive"", ""TableTemplateId"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_event_id, p_label, p_capacity, p_shape, p_color,
        p_price_cents, p_platform_fee_cents, true, p_template_id, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_table(
    p_event_table_id uuid, p_event_id uuid, p_label text,
    p_grid_row int, p_grid_col int, p_sort_order int
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO tables (""Id"", ""EventTableId"", ""EventId"", ""Label"", ""GridRow"", ""GridCol"",
        ""SortOrder"", ""IsActive"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_event_table_id, p_event_id, p_label,
        p_grid_row, p_grid_col, p_sort_order, true, 'Available', now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_lock_table(
    p_user_id uuid, p_event_id uuid, p_table_id uuid, p_hold_minutes int
) RETURNS TABLE(""Id"" uuid, ""Label"" text, ""LockExpiresAt"" timestamptz) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE tables SET
        ""Status"" = 'Locked', ""LockedByUserId"" = p_user_id,
        ""LockExpiresAt"" = now() + (p_hold_minutes || ' minutes')::interval,
        ""UpdatedAt"" = now()
    WHERE tables.""Id"" = p_table_id AND tables.""EventId"" = p_event_id
      AND tables.""Status"" = 'Available' AND tables.""IsActive"" = true
    RETURNING tables.""Id"", tables.""Label"", tables.""LockExpiresAt"";
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_release_table_lock(
    p_user_id uuid, p_event_id uuid, p_table_id uuid
) RETURNS bool LANGUAGE plpgsql AS $$
BEGIN
    UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_table_id AND ""EventId"" = p_event_id
      AND ""LockedByUserId"" = p_user_id AND ""Status"" = 'Locked';
    RETURN FOUND;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_mark_table_booked(p_table_id uuid) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE tables SET ""Status"" = 'Booked', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_table_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_cleanup_expired_locks() RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_count int;
BEGIN
    UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
        ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
    WHERE ""Status"" = 'Locked' AND ""LockExpiresAt"" < now();
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
");

            // ─── BOOKING STORED PROCEDURES ────────────────────────────────────────────────

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
        ""SeatsReserved"", ""EventTicketTypeId"", ""SubtotalCents"", ""FeeCents"", ""TotalCents"",
        ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_number, p_status, p_user_id, p_event_id, p_table_id,
        p_seats, p_event_ticket_type_id, p_subtotal_cents, p_fee_cents, p_total_cents,
        now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_confirm_booking(p_booking_id uuid, p_qr_token text)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_table_id uuid; v_seats int; v_seat int;
BEGIN
    UPDATE bookings SET ""Status"" = 'Paid', ""QrToken"" = p_qr_token, ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id AND ""Status"" = 'Pending'
    RETURNING ""TableId"", ""SeatsReserved"" INTO v_table_id, v_seats;
    IF v_table_id IS NOT NULL THEN
        PERFORM sp_mark_table_booked(v_table_id);
    END IF;
    v_seats := COALESCE(v_seats, 1);
    FOR v_seat IN 1..v_seats LOOP
        INSERT INTO booking_tickets (""Id"", ""BookingId"", ""TicketCode"", ""QrToken"",
            ""SeatNumber"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
        VALUES (gen_random_uuid(), p_booking_id,
            'TKT-' || UPPER(SUBSTRING(gen_random_uuid()::text FROM 1 FOR 8)),
            encode(gen_random_bytes(32), 'hex'),
            v_seat, 'Unassigned', now(), now());
    END LOOP;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_cancel_booking(p_booking_id uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_table_id uuid;
BEGIN
    UPDATE bookings SET ""Status"" = 'Cancelled', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id RETURNING ""TableId"" INTO v_table_id;
    IF v_table_id IS NOT NULL THEN
        UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
            ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
        WHERE ""Id"" = v_table_id AND ""Status"" IN ('Locked','Booked');
    END IF;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_refund_booking(p_booking_id uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_table_id uuid;
BEGIN
    UPDATE bookings SET ""Status"" = 'Refunded', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_booking_id RETURNING ""TableId"" INTO v_table_id;
    UPDATE payments SET ""Status"" = 'Refunded', ""RefundedAt"" = now(), ""UpdatedAt"" = now()
    WHERE ""BookingId"" = p_booking_id;
    IF v_table_id IS NOT NULL THEN
        UPDATE tables SET ""Status"" = 'Available', ""LockedByUserId"" = NULL,
            ""LockExpiresAt"" = NULL, ""UpdatedAt"" = now()
        WHERE ""Id"" = v_table_id;
    END IF;
END; $$;
");

            // ─── TICKET STORED PROCEDURES ─────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_invite_ticket(
    p_ticket_id uuid, p_invite_hash text, p_email text, p_expires_at timestamptz
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE booking_tickets SET
        ""InviteTokenHash"" = p_invite_hash, ""InvitedEmail"" = p_email,
        ""InviteSentAt"" = now(), ""InviteExpiresAt"" = p_expires_at,
        ""Status"" = 'Invited', ""UpdatedAt"" = now()
    WHERE ""Id"" = p_ticket_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_claim_ticket(p_invite_hash text, p_guest_user_id uuid)
RETURNS TABLE(""TicketId"" uuid, ""BookingId"" uuid) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    UPDATE booking_tickets SET
        ""GuestUserId"" = p_guest_user_id, ""ClaimedAt"" = now(),
        ""Status"" = 'Claimed', ""UpdatedAt"" = now()
    WHERE ""InviteTokenHash"" = p_invite_hash AND ""Status"" = 'Invited' AND ""InviteExpiresAt"" > now()
    RETURNING booking_tickets.""Id"" AS ""TicketId"", booking_tickets.""BookingId"";
END; $$;
");

            // ─── PAYMENT STORED PROCEDURES ────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_payment(
    p_booking_id uuid, p_intent_id text, p_amount_cents int, p_currency text DEFAULT 'usd'
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO payments (""Id"", ""BookingId"", ""PaymentIntentId"", ""Status"",
        ""AmountCents"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_booking_id, p_intent_id, 'RequiresConfirmation',
        p_amount_cents, p_currency, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_payment_status(p_intent_id text, p_status text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE payments SET
        ""Status"" = p_status,
        ""PaidAt"" = CASE WHEN p_status IN ('Succeeded','Refunded') AND ""PaidAt"" IS NULL THEN now() ELSE ""PaidAt"" END,
        ""RefundedAt"" = CASE WHEN p_status = 'Refunded' THEN now() ELSE ""RefundedAt"" END,
        ""UpdatedAt"" = now()
    WHERE ""PaymentIntentId"" = p_intent_id;
END; $$;
");

            // ─── IMAGE STORED PROCEDURES ──────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_image(
    p_entity_type text, p_entity_id uuid, p_storage_key text, p_original_name text,
    p_size_bytes int, p_width int, p_height int,
    p_is_primary bool, p_sort_order int, p_uploaded_by uuid
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    IF p_is_primary THEN
        UPDATE images SET ""IsPrimary"" = false, ""UpdatedAt"" = now()
        WHERE ""EntityType"" = p_entity_type AND ""EntityId"" = p_entity_id AND ""IsPrimary"" = true;
    END IF;
    INSERT INTO images (""Id"", ""EntityType"", ""EntityId"", ""StorageKey"", ""OriginalName"",
        ""SizeBytes"", ""Width"", ""Height"", ""IsPrimary"", ""SortOrder"", ""UploadedById"",
        ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_entity_type, p_entity_id, p_storage_key, p_original_name,
        p_size_bytes, p_width, p_height, p_is_primary, p_sort_order, p_uploaded_by,
        now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_delete_image(p_image_id uuid) RETURNS text LANGUAGE plpgsql AS $$
DECLARE v_key text;
BEGIN
    DELETE FROM images WHERE ""Id"" = p_image_id RETURNING ""StorageKey"" INTO v_key;
    RETURN v_key;
END; $$;
");

            // ─── SETTINGS STORED PROCEDURES ───────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_upsert_setting(
    p_key text, p_encrypted_value text, p_description text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO app_settings (""Id"", ""Key"", ""EncryptedValue"", ""Description"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_key, p_encrypted_value, p_description, now(), now())
    ON CONFLICT (""Key"") DO UPDATE SET
        ""EncryptedValue"" = EXCLUDED.""EncryptedValue"",
        ""Description"" = COALESCE(EXCLUDED.""Description"", app_settings.""Description""),
        ""UpdatedAt"" = now();
END; $$;
");

            // ─── LOGGING STORED PROCEDURES ────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_admin_log(
    p_action text, p_actor_id uuid, p_actor_email text, p_actor_role text,
    p_entity_type text, p_entity_id uuid, p_description text,
    p_metadata_json text, p_ip text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO admin_logs (""Id"", ""Timestamp"", ""Action"", ""ActorId"", ""ActorEmail"", ""ActorRole"",
        ""EntityType"", ""EntityId"", ""Description"", ""MetadataJson"", ""IpAddress"")
    VALUES (gen_random_uuid(), now(), p_action, p_actor_id, p_actor_email, p_actor_role,
        p_entity_type, p_entity_id, p_description, p_metadata_json, p_ip)
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_developer_log(
    p_severity text, p_message text, p_exception_type text, p_stack_trace text,
    p_request_path text, p_request_method text, p_status_code int,
    p_user_id uuid, p_ip text, p_correlation_id text, p_metadata_json text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO developer_logs (""Id"", ""Timestamp"", ""Severity"", ""Message"", ""ExceptionType"",
        ""StackTrace"", ""RequestPath"", ""RequestMethod"", ""StatusCode"", ""UserId"",
        ""IpAddress"", ""CorrelationId"", ""MetadataJson"")
    VALUES (gen_random_uuid(), now(), p_severity, p_message, p_exception_type, p_stack_trace,
        p_request_path, p_request_method, p_status_code, p_user_id,
        p_ip, p_correlation_id, p_metadata_json)
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_system_log(
    p_category text, p_action text, p_source text,
    p_entity_type text, p_entity_id uuid,
    p_before_json text, p_after_json text,
    p_actor_id uuid, p_correlation_id text, p_duration_ms bigint, p_metadata_json text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO system_logs (""Id"", ""Timestamp"", ""Category"", ""Action"", ""Source"",
        ""EntityType"", ""EntityId"", ""BeforeJson"", ""AfterJson"", ""ActorId"",
        ""CorrelationId"", ""DurationMs"", ""MetadataJson"")
    VALUES (gen_random_uuid(), now(), p_category, p_action, p_source,
        p_entity_type, p_entity_id, p_before_json, p_after_json, p_actor_id,
        p_correlation_id, p_duration_ms, p_metadata_json)
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_email_log(
    p_recipient text, p_subject text, p_body text, p_status text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO email_logs (""Id"", ""Timestamp"", ""Recipient"", ""Subject"", ""Body"", ""Status"")
    VALUES (gen_random_uuid(), now(), p_recipient, p_subject, p_body, p_status)
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_cleanup_old_logs(
    p_dev_days int, p_admin_days int, p_system_days int
) RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_total int := 0; v_count int;
BEGIN
    DELETE FROM developer_logs WHERE ""Timestamp"" < now() - (p_dev_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;
    DELETE FROM admin_logs WHERE ""Timestamp"" < now() - (p_admin_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;
    DELETE FROM system_logs WHERE ""Timestamp"" < now() - (p_system_days || ' days')::interval;
    GET DIAGNOSTICS v_count = ROW_COUNT; v_total := v_total + v_count;
    RETURN v_total;
END; $$;
");

            // ─── FEEDBACK STORED PROCEDURE ────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_feedback(
    p_name text, p_email text, p_type text, p_message text, p_rating int,
    p_user_id uuid, p_user_agent text, p_ip text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO feedbacks (""Id"", ""Name"", ""Email"", ""Type"", ""Message"", ""Rating"",
        ""UserId"", ""UserAgent"", ""IpAddress"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_name, p_email, p_type, p_message, p_rating,
        p_user_id, p_user_agent, p_ip, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            // ─── ADMIN USER STORED PROCEDURES ────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_admin_user(
    p_email text, p_email_hash text, p_first_name text, p_last_name text,
    p_password_hash text, p_role text
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO admin_users (""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
        ""PasswordHash"", ""Role"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (p_email, p_email_hash, p_first_name, p_last_name,
        p_password_hash, p_role, true, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_admin_user(
    p_id uuid, p_first_name text DEFAULT NULL, p_last_name text DEFAULT NULL,
    p_phone text DEFAULT NULL, p_role text DEFAULT NULL,
    p_is_active boolean DEFAULT NULL, p_avatar_path text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET
        ""FirstName"" = COALESCE(p_first_name, ""FirstName""),
        ""LastName"" = COALESCE(p_last_name, ""LastName""),
        ""Phone"" = COALESCE(p_phone, ""Phone""),
        ""Role"" = COALESCE(p_role, ""Role""),
        ""IsActive"" = COALESCE(p_is_active, ""IsActive""),
        ""AvatarPath"" = COALESCE(p_avatar_path, ""AvatarPath""),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_admin_password(p_id uuid, p_password_hash text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET ""PasswordHash"" = p_password_hash, ""UpdatedAt"" = now() WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_admin_last_login(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now() WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_admin_device_session(
    p_admin_user_id uuid, p_session_hash text, p_fingerprint text,
    p_device_name text, p_ip text, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO device_sessions (""AdminUserId"", ""SessionHash"", ""DeviceFingerprint"",
        ""DeviceName"", ""IpAddress"", ""LastActivityAt"", ""ExpiresAt"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (p_admin_user_id, p_session_hash, p_fingerprint,
        p_device_name, p_ip, now(), p_expires_at, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_revoke_all_admin_sessions(
    p_admin_user_id uuid, p_except_hash text DEFAULT NULL
) RETURNS int LANGUAGE plpgsql AS $$
DECLARE v_count int;
BEGIN
    UPDATE device_sessions SET ""RevokedAt"" = now()
    WHERE ""AdminUserId"" = p_admin_user_id AND ""RevokedAt"" IS NULL
      AND (p_except_hash IS NULL OR ""SessionHash"" <> p_except_hash);
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END; $$;
");

            // ─── AUDIT TRIGGER ────────────────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_audit_trigger() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        INSERT INTO system_logs (""Id"", ""Timestamp"", ""Category"", ""Action"", ""Source"",
            ""EntityType"", ""EntityId"", ""BeforeJson"", ""AfterJson"")
        VALUES (gen_random_uuid(), now(), 'EntityChange', 'Delete', TG_TABLE_NAME,
            TG_TABLE_NAME, (OLD.""Id"")::uuid, row_to_json(OLD)::text, NULL);
        RETURN OLD;
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO system_logs (""Id"", ""Timestamp"", ""Category"", ""Action"", ""Source"",
            ""EntityType"", ""EntityId"", ""BeforeJson"", ""AfterJson"")
        VALUES (gen_random_uuid(), now(), 'EntityChange', 'Update', TG_TABLE_NAME,
            TG_TABLE_NAME, (NEW.""Id"")::uuid, row_to_json(OLD)::text, row_to_json(NEW)::text);
        RETURN NEW;
    ELSIF TG_OP = 'INSERT' THEN
        INSERT INTO system_logs (""Id"", ""Timestamp"", ""Category"", ""Action"", ""Source"",
            ""EntityType"", ""EntityId"", ""BeforeJson"", ""AfterJson"")
        VALUES (gen_random_uuid(), now(), 'EntityChange', 'Insert', TG_TABLE_NAME,
            TG_TABLE_NAME, (NEW.""Id"")::uuid, NULL, row_to_json(NEW)::text);
        RETURN NEW;
    END IF;
    RETURN NULL;
END; $$;
");

            var auditTables = new[]
            {
                "users", "addresses", "events", "venues", "event_tables", "tables",
                "bookings", "booking_tickets", "payments", "images", "feedbacks",
                "magic_link_tokens", "device_sessions", "app_settings", "table_templates"
            };
            foreach (var table in auditTables)
            {
                migrationBuilder.Sql($@"
CREATE TRIGGER trg_{table}_audit AFTER INSERT OR UPDATE OR DELETE ON {table}
FOR EACH ROW EXECUTE FUNCTION fn_audit_trigger();
");
            }

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_logs");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "booking_tickets");

            migrationBuilder.DropTable(
                name: "developer_logs");

            migrationBuilder.DropTable(
                name: "device_sessions");

            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "feedbacks");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "magic_link_tokens");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "event_ticket_types");

            migrationBuilder.DropTable(
                name: "tables");

            migrationBuilder.DropTable(
                name: "event_tables");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "table_templates");

            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropTable(
                name: "addresses");
        }
    }
}
