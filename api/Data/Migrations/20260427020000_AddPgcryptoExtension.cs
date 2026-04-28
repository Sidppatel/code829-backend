using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Installs the pgcrypto extension into the <c>extensions</c> schema. Required
    /// by sp_confirm_purchase and any SP that calls <c>gen_random_bytes</c> /
    /// <c>digest</c>. The squashed InitialCreate only declared pg_trgm.
    /// </remarks>
    public partial class AddPgcryptoExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("AddPgcryptoExtension.Up.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MigrationSqlLoader.Load("AddPgcryptoExtension.Down.sql"));
        }
    }
}
