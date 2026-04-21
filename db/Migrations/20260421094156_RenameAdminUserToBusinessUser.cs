using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminUserToBusinessUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_device_sessions_admin_users_AdminUserId",
                table: "device_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_events_admin_users_AdminUserId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_invitations_admin_users_InvitedByAdminUserId",
                table: "invitations");

            migrationBuilder.DropTable(
                name: "admin_logs");

            migrationBuilder.DropTable(
                name: "admin_password_reset_tokens");

            migrationBuilder.DropTable(
                name: "admin_user_events");

            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions");

            migrationBuilder.RenameColumn(
                name: "InvitedByAdminUserId",
                table: "invitations",
                newName: "InvitedByBusinessUserId");

            migrationBuilder.RenameIndex(
                name: "IX_invitations_InvitedByAdminUserId",
                table: "invitations",
                newName: "IX_invitations_InvitedByBusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "events",
                newName: "BusinessUserId");

            migrationBuilder.RenameIndex(
                name: "IX_events_AdminUserId",
                table: "events",
                newName: "IX_events_BusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "device_sessions",
                newName: "BusinessUserId");

            migrationBuilder.RenameIndex(
                name: "IX_device_sessions_AdminUserId",
                table: "device_sessions",
                newName: "IX_device_sessions_BusinessUserId");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "developer_logs",
                newName: "BusinessUserId");

            migrationBuilder.CreateTable(
                name: "business_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "business_users",
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
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StripeConnectedAccountId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_users", x => x.Id);
                    table.CheckConstraint("CK_business_users_Role", "\"Role\" IN ('Staff','Admin','Developer')");
                    table.ForeignKey(
                        name: "FK_business_users_images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "business_password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_business_password_reset_tokens", x => x.Id);
                    table.CheckConstraint("CK_business_password_reset_tokens_Usage", "(\"IsUsed\" = false AND \"UsedAt\" IS NULL) OR (\"IsUsed\" = true AND \"UsedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_business_password_reset_tokens_business_users_BusinessUserId",
                        column: x => x.BusinessUserId,
                        principalTable: "business_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_user_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByBusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_user_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_user_events_business_users_AssignedByBusinessUserId",
                        column: x => x.AssignedByBusinessUserId,
                        principalTable: "business_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_business_user_events_business_users_BusinessUserId",
                        column: x => x.BusinessUserId,
                        principalTable: "business_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_business_user_events_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions",
                sql: "(\"UserId\" IS NOT NULL AND \"BusinessUserId\" IS NULL) OR (\"UserId\" IS NULL AND \"BusinessUserId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_business_logs_Action",
                table: "business_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_business_logs_Timestamp",
                table: "business_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_business_password_reset_tokens_BusinessUserId",
                table: "business_password_reset_tokens",
                column: "BusinessUserId");

            migrationBuilder.CreateIndex(
                name: "IX_business_password_reset_tokens_ExpiresAt",
                table: "business_password_reset_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_business_password_reset_tokens_TokenHash",
                table: "business_password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_user_events_AssignedByBusinessUserId",
                table: "business_user_events",
                column: "AssignedByBusinessUserId");

            migrationBuilder.CreateIndex(
                name: "IX_business_user_events_BusinessUserId_EventId",
                table: "business_user_events",
                columns: new[] { "BusinessUserId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_user_events_EventId",
                table: "business_user_events",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_business_users_Email",
                table: "business_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_users_EmailHash",
                table: "business_users",
                column: "EmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_users_ImageId",
                table: "business_users",
                column: "ImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_device_sessions_business_users_BusinessUserId",
                table: "device_sessions",
                column: "BusinessUserId",
                principalTable: "business_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_events_business_users_BusinessUserId",
                table: "events",
                column: "BusinessUserId",
                principalTable: "business_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invitations_business_users_InvitedByBusinessUserId",
                table: "invitations",
                column: "InvitedByBusinessUserId",
                principalTable: "business_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_device_sessions_business_users_BusinessUserId",
                table: "device_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_events_business_users_BusinessUserId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_invitations_business_users_InvitedByBusinessUserId",
                table: "invitations");

            migrationBuilder.DropTable(
                name: "business_logs");

            migrationBuilder.DropTable(
                name: "business_password_reset_tokens");

            migrationBuilder.DropTable(
                name: "business_user_events");

            migrationBuilder.DropTable(
                name: "business_users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions");

            migrationBuilder.RenameColumn(
                name: "InvitedByBusinessUserId",
                table: "invitations",
                newName: "InvitedByAdminUserId");

            migrationBuilder.RenameIndex(
                name: "IX_invitations_InvitedByBusinessUserId",
                table: "invitations",
                newName: "IX_invitations_InvitedByAdminUserId");

            migrationBuilder.RenameColumn(
                name: "BusinessUserId",
                table: "events",
                newName: "AdminUserId");

            migrationBuilder.RenameIndex(
                name: "IX_events_BusinessUserId",
                table: "events",
                newName: "IX_events_AdminUserId");

            migrationBuilder.RenameColumn(
                name: "BusinessUserId",
                table: "device_sessions",
                newName: "AdminUserId");

            migrationBuilder.RenameIndex(
                name: "IX_device_sessions_BusinessUserId",
                table: "device_sessions",
                newName: "IX_device_sessions_AdminUserId");

            migrationBuilder.RenameColumn(
                name: "BusinessUserId",
                table: "developer_logs",
                newName: "AdminUserId");

            migrationBuilder.CreateTable(
                name: "admin_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StripeConnectedAccountId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                    table.CheckConstraint("CK_admin_users_Role", "\"Role\" IN ('Staff','Admin','Developer')");
                    table.ForeignKey(
                        name: "FK_admin_users_images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "admin_password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "admin_user_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_user_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_user_events_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_user_events_admin_users_AssignedByAdminUserId",
                        column: x => x.AssignedByAdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_admin_user_events_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_device_sessions_UserType",
                table: "device_sessions",
                sql: "(\"UserId\" IS NOT NULL AND \"AdminUserId\" IS NULL) OR (\"UserId\" IS NULL AND \"AdminUserId\" IS NOT NULL)");

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
                name: "IX_admin_user_events_AdminUserId_EventId",
                table: "admin_user_events",
                columns: new[] { "AdminUserId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_user_events_AssignedByAdminUserId",
                table: "admin_user_events",
                column: "AssignedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_user_events_EventId",
                table: "admin_user_events",
                column: "EventId");

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
                name: "IX_admin_users_ImageId",
                table: "admin_users",
                column: "ImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_device_sessions_admin_users_AdminUserId",
                table: "device_sessions",
                column: "AdminUserId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_events_admin_users_AdminUserId",
                table: "events",
                column: "AdminUserId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invitations_admin_users_InvitedByAdminUserId",
                table: "invitations",
                column: "InvitedByAdminUserId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
