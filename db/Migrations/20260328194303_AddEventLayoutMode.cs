using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddEventLayoutMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LayoutMode",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxCapacity",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformFeePercent",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutMode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MaxCapacity",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PlatformFeePercent",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "events");
        }
    }
}
