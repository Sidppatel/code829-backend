using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_password_reset_tokens");
        }
    }
}
