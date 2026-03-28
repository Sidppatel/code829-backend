using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddMagicLinkTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "magic_link_tokens");
        }
    }
}
