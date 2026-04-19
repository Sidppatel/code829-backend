using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddLogQuerySps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_admin_logs.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_email_logs.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_developer_logs.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_system_logs.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_admin_logs CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_count_admin_logs CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_email_logs CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_count_email_logs CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_developer_logs CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_count_developer_logs CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_system_logs CASCADE;");
        }
    }
}
