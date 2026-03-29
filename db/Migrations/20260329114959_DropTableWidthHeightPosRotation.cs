using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class DropTableWidthHeightPosRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Height",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PosX",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PosY",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "tables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Height",
                table: "tables",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PosX",
                table: "tables",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PosY",
                table: "tables",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rotation",
                table: "tables",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Width",
                table: "tables",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
