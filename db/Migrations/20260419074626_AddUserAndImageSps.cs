using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndImageSps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_set_user_active.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_delete_user.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_get_primary_image_key.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_set_user_active CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_delete_user CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_primary_image_key CASCADE;");
        }
    }
}
