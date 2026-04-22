using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_email_verification_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_email_verification_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_email_verification_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_password_reset_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_email_verification_tokens_ExpiresAt",
                table: "user_email_verification_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_email_verification_tokens_TokenHash",
                table: "user_email_verification_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_email_verification_tokens_UserId",
                table: "user_email_verification_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_password_reset_tokens_ExpiresAt",
                table: "user_password_reset_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_password_reset_tokens_TokenHash",
                table: "user_password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_password_reset_tokens_UserId",
                table: "user_password_reset_tokens",
                column: "UserId");

            // Install the new email+password stored procedures. These reference the
            // two token tables created above, so they must be loaded after CreateTable.
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_signup_user.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_user_by_email_for_signin.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_user_password.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_user_password_reset_token.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_consume_user_password_reset_token.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_create_user_email_verification_token.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_consume_user_email_verification_token.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop SPs first — the two token-table ones reference tables we're about to drop.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_consume_user_email_verification_token(text) CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_user_email_verification_token(uuid, text, timestamptz, text) CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_consume_user_password_reset_token(text) CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_user_password_reset_token(uuid, text, timestamptz, text) CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_user_password(uuid, text) CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_user_by_email_for_signin(text) CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_signup_user(text, text, text, text, text) CASCADE;");

            migrationBuilder.DropTable(
                name: "user_email_verification_tokens");

            migrationBuilder.DropTable(
                name: "user_password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");
        }
    }
}
