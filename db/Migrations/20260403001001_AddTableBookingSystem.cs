using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddTableBookingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_Status",
                table: "bookings");

            migrationBuilder.AddColumn<DateTime>(
                name: "LockExpiresAt",
                table: "tables",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockedByUserId",
                table: "tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "tables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Available");

            migrationBuilder.AddColumn<int>(
                name: "SeatsReserved",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TableId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_EventId_Status",
                table: "tables",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tables_LockedByUserId",
                table: "tables",
                column: "LockedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_AvailableNoLock",
                table: "tables",
                sql: "\"Status\" <> 'Available' OR (\"LockedByUserId\" IS NULL AND \"LockExpiresAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_LockedRequiresOwner",
                table: "tables",
                sql: "\"Status\" <> 'Locked' OR (\"LockedByUserId\" IS NOT NULL AND \"LockExpiresAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tables_Status",
                table: "tables",
                sql: "\"Status\" IN ('Available','Locked','Booked')");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TableId",
                table: "bookings",
                column: "TableId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_SeatsReserved",
                table: "bookings",
                sql: "\"SeatsReserved\" IS NULL OR \"SeatsReserved\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_Status",
                table: "bookings",
                sql: "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_tables_TableId",
                table: "bookings",
                column: "TableId",
                principalTable: "tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tables_users_LockedByUserId",
                table: "tables",
                column: "LockedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_tables_TableId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_tables_users_LockedByUserId",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_EventId_Status",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_tables_LockedByUserId",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_AvailableNoLock",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_LockedRequiresOwner",
                table: "tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tables_Status",
                table: "tables");

            migrationBuilder.DropIndex(
                name: "IX_bookings_TableId",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_SeatsReserved",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_Status",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LockExpiresAt",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "LockedByUserId",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "SeatsReserved",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "bookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_Status",
                table: "bookings",
                sql: "\"Status\" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded')");
        }
    }
}
