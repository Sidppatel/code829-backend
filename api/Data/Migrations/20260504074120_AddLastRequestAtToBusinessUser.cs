using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddLastRequestAtToBusinessUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRequestAt",
                table: "business_users",
                type: "timestamp with time zone",
                nullable: true);

            // Refresh SPs to write the new column. Both use CREATE OR REPLACE — idempotent.
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_session_activity.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_update_business_user_last_login.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore prior SP bodies BEFORE dropping the column so a rolled-back deploy
            // doesn't leave SPs referencing a missing column.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_session_activity(p_session_hash text) RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE device_sessions SET ""LastActivityAt"" = now() WHERE ""SessionHash"" = p_session_hash;
END; $$;");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_update_business_user_last_login(p_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE business_users SET ""LastLoginAt"" = now(), ""UpdatedAt"" = now() WHERE ""Id"" = p_id;
END; $$;");

            migrationBuilder.DropColumn(
                name: "LastRequestAt",
                table: "business_users");
        }
    }
}
