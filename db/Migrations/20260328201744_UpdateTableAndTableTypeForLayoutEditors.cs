using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTableAndTableTypeForLayoutEditors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "HeightPx",
                table: "table_types");

            migrationBuilder.RenameColumn(
                name: "Y",
                table: "tables",
                newName: "Width");

            migrationBuilder.RenameColumn(
                name: "X",
                table: "tables",
                newName: "Height");

            migrationBuilder.RenameColumn(
                name: "WidthPx",
                table: "table_types",
                newName: "DefaultPriceCents");

            migrationBuilder.RenameColumn(
                name: "Shape",
                table: "table_types",
                newName: "DefaultShape");

            migrationBuilder.RenameColumn(
                name: "SeatsPerTable",
                table: "table_types",
                newName: "DefaultCapacity");

            migrationBuilder.AlterColumn<Guid>(
                name: "TableTypeId",
                table: "tables",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "tables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridCol",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridRow",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "tables",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.AddColumn<int>(
                name: "PriceCents",
                table: "tables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriceOverrideCents",
                table: "tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "tables",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "tables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "VenueId",
                table: "table_types",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DefaultColor",
                table: "table_types",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "table_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EditorMode",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridCols",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridRows",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId",
                table: "tables",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables",
                column: "EventId",
                principalTable: "events",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_events_EventId",
                table: "tables");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_EventId",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "GridCol",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "GridRow",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PosX",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PosY",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PriceCents",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PriceOverrideCents",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "DefaultColor",
                table: "table_types");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "table_types");

            migrationBuilder.DropColumn(
                name: "EditorMode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GridCols",
                table: "events");

            migrationBuilder.DropColumn(
                name: "GridRows",
                table: "events");

            migrationBuilder.RenameColumn(
                name: "Width",
                table: "tables",
                newName: "Y");

            migrationBuilder.RenameColumn(
                name: "Height",
                table: "tables",
                newName: "X");

            migrationBuilder.RenameColumn(
                name: "DefaultShape",
                table: "table_types",
                newName: "Shape");

            migrationBuilder.RenameColumn(
                name: "DefaultPriceCents",
                table: "table_types",
                newName: "WidthPx");

            migrationBuilder.RenameColumn(
                name: "DefaultCapacity",
                table: "table_types",
                newName: "SeatsPerTable");

            migrationBuilder.AlterColumn<Guid>(
                name: "TableTypeId",
                table: "tables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VenueId",
                table: "table_types",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeightPx",
                table: "table_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_table_types_venues_VenueId",
                table: "table_types",
                column: "VenueId",
                principalTable: "venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_table_types_TableTypeId",
                table: "tables",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
