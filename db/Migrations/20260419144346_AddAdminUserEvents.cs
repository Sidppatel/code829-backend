using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminUserEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_user_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
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

            // (Re)load views + procedures so the new view v_admin_user_events
            // and sp_* functions depending on admin_user_events exist after
            // this migration runs. All SPs/views use CREATE OR REPLACE, so
            // re-running the full set is idempotent.
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.Procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_admin_user_events;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_assign_admin_user_event(uuid, uuid, uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_unassign_admin_user_event(uuid, uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_admin_user_event_exists(uuid, uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_staff_can_access_event(uuid, uuid, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_staff_for_event(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_events_for_staff(uuid, int);");

            migrationBuilder.DropTable(
                name: "admin_user_events");
        }
    }
}
