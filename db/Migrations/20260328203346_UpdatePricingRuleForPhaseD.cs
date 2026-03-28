using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePricingRuleForPhaseD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "pricing_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCount",
                table: "pricing_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "pricing_rules",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PriceCents",
                table: "pricing_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "pricing_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TableTypeId",
                table: "pricing_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "pricing_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UsedCount",
                table: "pricing_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "pricing_rules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "pricing_rules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_TableTypeId",
                table: "pricing_rules",
                column: "TableTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_rules_table_types_TableTypeId",
                table: "pricing_rules",
                column: "TableTypeId",
                principalTable: "table_types",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pricing_rules_table_types_TableTypeId",
                table: "pricing_rules");

            migrationBuilder.DropIndex(
                name: "IX_pricing_rules_TableTypeId",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "MaxCount",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "PriceCents",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "TableTypeId",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "UsedCount",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "pricing_rules");
        }
    }
}
