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
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    Value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
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
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    UploaderType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_images", x => x.Id);
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
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "admin_password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_password_reset_tokens", x => x.Id);
                    table.CheckConstraint("CK_admin_password_reset_tokens_Usage", "(\"IsUsed\" = false AND \"UsedAt\" IS NULL) OR (\"IsUsed\" = true AND \"UsedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_admin_password_reset_tokens_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                        name: "FK_events_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
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
                name: "purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PurchaseNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_purchases", x => x.Id);
                    table.CheckConstraint("CK_purchases_FeeCents", "\"FeeCents\" >= 0");
                    table.CheckConstraint("CK_purchases_SeatsReserved", "\"SeatsReserved\" IS NULL OR \"SeatsReserved\" > 0");
                    table.CheckConstraint("CK_purchases_Status", "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                    table.CheckConstraint("CK_purchases_SubtotalCents", "\"SubtotalCents\" >= 0");
                    table.CheckConstraint("CK_purchases_TotalCents", "\"TotalCents\" >= 0");
                    table.CheckConstraint("CK_purchases_TotalFormula", "\"TotalCents\" = \"SubtotalCents\" + \"FeeCents\"");
                    table.ForeignKey(
                        name: "FK_purchases_event_ticket_types_EventTicketTypeId",
                        column: x => x.EventTicketTypeId,
                        principalTable: "event_ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchases_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_tables_TableId",
                        column: x => x.TableId,
                        principalTable: "tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchases_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TicketCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QrToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_purchase_tickets", x => x.Id);
                    table.CheckConstraint("CK_purchase_tickets_SeatNumber", "\"SeatNumber\" > 0");
                    table.CheckConstraint("CK_purchase_tickets_Status", "\"Status\" IN ('Unassigned','Invited','Claimed','CheckedIn')");
                    table.ForeignKey(
                        name: "FK_purchase_tickets_purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_tickets_users_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stripe_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    TransferAmountCents = table.Column<int>(type: "integer", nullable: true),
                    TaxCalculationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TaxTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TotalChargedCents = table.Column<int>(type: "integer", nullable: true),
                    TaxAmountCents = table.Column<int>(type: "integer", nullable: true),
                    StripeFeesCents = table.Column<int>(type: "integer", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stripe_transactions", x => x.Id);
                    table.CheckConstraint("CK_stripe_transactions_AmountCents", "\"AmountCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_Currency", "\"Currency\" IN ('usd')");
                    table.CheckConstraint("CK_stripe_transactions_NotRefundedNoRefundDate", "\"Status\" = 'Refunded' OR \"RefundedAt\" IS NULL");
                    table.CheckConstraint("CK_stripe_transactions_PaidLifecycle", "\"Status\" NOT IN ('Succeeded','Refunded') OR \"PaidAt\" IS NOT NULL");
                    table.CheckConstraint("CK_stripe_transactions_PendingNoPaidDate", "\"Status\" NOT IN ('RequiresConfirmation','Failed') OR \"PaidAt\" IS NULL");
                    table.CheckConstraint("CK_stripe_transactions_RefundLifecycle", "\"Status\" <> 'Refunded' OR \"RefundedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_stripe_transactions_Status", "\"Status\" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                    table.CheckConstraint("CK_stripe_transactions_StripeFees", "\"StripeFeesCents\" IS NULL OR \"StripeFeesCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TaxAmount", "\"TaxAmountCents\" IS NULL OR \"TaxAmountCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TotalCharged", "\"TotalChargedCents\" IS NULL OR \"TotalChargedCents\" >= 0");
                    table.CheckConstraint("CK_stripe_transactions_TransferAmount", "\"TransferAmountCents\" IS NULL OR \"TransferAmountCents\" >= 0");
                    table.ForeignKey(
                        name: "FK_stripe_transactions_purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "purchases",
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
                name: "IX_admin_password_reset_tokens_AdminUserId",
                table: "admin_password_reset_tokens",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_password_reset_tokens_ExpiresAt",
                table: "admin_password_reset_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_password_reset_tokens_TokenHash",
                table: "admin_password_reset_tokens",
                column: "TokenHash",
                unique: true);

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
                name: "IX_events_AdminUserId",
                table: "events",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_events_Category",
                table: "events",
                column: "Category");

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
                name: "IX_purchase_tickets_GuestUserId",
                table: "purchase_tickets",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_tickets_InviteTokenHash",
                table: "purchase_tickets",
                column: "InviteTokenHash",
                unique: true,
                filter: "\"InviteTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_tickets_PurchaseId_SeatNumber",
                table: "purchase_tickets",
                columns: new[] { "PurchaseId", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_tickets_QrToken",
                table: "purchase_tickets",
                column: "QrToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_EventId_Status",
                table: "purchases",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_EventTicketTypeId",
                table: "purchases",
                column: "EventTicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_PurchaseNumber",
                table: "purchases",
                column: "PurchaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_QrToken",
                table: "purchases",
                column: "QrToken",
                unique: true,
                filter: "\"QrToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_Status",
                table: "purchases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_TableId",
                table: "purchases",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_UserId",
                table: "purchases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_UserId_CreatedAt",
                table: "purchases",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transactions_PaymentIntentId",
                table: "stripe_transactions",
                column: "PaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transactions_PurchaseId",
                table: "stripe_transactions",
                column: "PurchaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stripe_transactions_Status_PaidAt",
                table: "stripe_transactions",
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

            // --- EXTENSIONS ---------------------------------------------------------------

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // --- PURCHASE TABLES (multi-table support) ------------------------------------

            migrationBuilder.Sql(@"
CREATE TABLE purchase_tables (
    ""PurchaseId"" uuid NOT NULL REFERENCES purchases(""Id"") ON DELETE CASCADE,
    ""TableId"" uuid NOT NULL REFERENCES tables(""Id"") ON DELETE CASCADE,
    PRIMARY KEY (""PurchaseId"", ""TableId"")
);
CREATE INDEX ""IX_purchase_tables_TableId"" ON purchase_tables (""TableId"");
");

            // --- VIEWS --------------------------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_events.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_event_summary.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_user_profile.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_tables.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_purchases.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_purchase_tickets.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_venues.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_event_tables_summary.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_event_ticket_types_summary.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_admin_users.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_device_sessions.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_invitations.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("v_feedbacks.sql"));

            // --- AUTH STORED PROCEDURES ---------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_magic_link.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_consume_magic_link.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_upsert_user.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_user_last_login.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_device_session.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_revoke_device_session.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_revoke_all_user_sessions.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_cleanup_expired_sessions.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_session_activity.sql"));

            // --- USER STORED PROCEDURES ---------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_user_profile.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_user_avatar.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_user_by_id.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_user_by_email.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_user_by_email_hash.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_users.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_user_exists_by_email.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_user_counts.sql"));

            // --- EVENT STORED PROCEDURES --------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_event.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_event.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_change_event_status.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_publish_scheduled_events.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_event.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_event_stats.sql"));

            // --- EVENT TICKET TYPE STORED PROCEDURES --------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_event_ticket_type.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_event_ticket_type.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_event_ticket_type.sql"));

            // --- VENUE STORED PROCEDURES --------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_venue.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_venue.sql"));

            // --- TABLE/LAYOUT STORED PROCEDURES ------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_event_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_event_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_event_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_event_table_by_id.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_event_tables_for_event.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_existing_event_table_template_ids.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_table_by_id.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_tables_for_event.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_lock_table.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_release_table_lock.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_mark_table_booked.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_cleanup_expired_locks.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_event_has_active_purchases.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_event_table_has_active_purchases.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_event_table_has_locked_tables.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_locked_table_ids.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_event_by_id_for_layout.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_event_grid.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_save_event_layout.sql"));

            // --- TABLE TEMPLATE STORED PROCEDURES -----------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_table_template.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_table_template.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_deactivate_table_template.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_table_template_by_id.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_active_table_templates.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_list_active_table_templates_by_ids.sql"));

            // --- PURCHASE STORED PROCEDURES ------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_purchase.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_confirm_purchase.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_cancel_purchase.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_refund_purchase.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_reserve_open_capacity.sql"));

            // --- TICKET STORED PROCEDURES -------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_invite_ticket.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_claim_ticket.sql"));

            // --- STRIPE TRANSACTION STORED PROCEDURES --------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_stripe_transaction.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_stripe_transaction_status.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_enrich_stripe_transaction.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_set_stripe_tax_transaction_id.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_stripe_transaction_by_intent.sql"));

            // --- IMAGE STORED PROCEDURES --------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_image.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_image.sql"));

            // --- SETTINGS STORED PROCEDURES -----------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_upsert_setting.sql"));

            // --- LOGGING STORED PROCEDURES ------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_admin_log.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_developer_log.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_system_log.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_email_log.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_cleanup_old_logs.sql"));

            // --- FEEDBACK STORED PROCEDURE ------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_feedback.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_feedback.sql"));

            // --- ADMIN USER STORED PROCEDURES --------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_admin_user.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_admin_user.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_admin_password.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_admin_last_login.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_admin_device_session.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_revoke_all_admin_sessions.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_admin_by_id.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_admin_by_email.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_admin_exists_by_email.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_increment_admin_failed_login.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_reset_admin_lockout.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_invitation.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_pending_invitation_by_email.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_invitation_by_token_hash.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_accept_invitation.sql"));

            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_revoke_invitation.sql"));

            // --- AUDIT TRIGGER ------------------------------------------------------------

            migrationBuilder.Sql(MigrationSqlLoader.Load("fn_audit_trigger.sql"));

            var auditTables = new[]
            {
                "users", "addresses", "events", "venues", "event_tables", "tables",
                "purchases", "purchase_tickets", "stripe_transactions", "images", "feedbacks",
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
                name: "admin_password_reset_tokens");

            migrationBuilder.DropTable(
                name: "app_settings");

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
                name: "purchase_tickets");

            migrationBuilder.DropTable(
                name: "stripe_transactions");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "purchases");

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
