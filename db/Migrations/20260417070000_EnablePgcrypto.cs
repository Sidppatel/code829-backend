using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <summary>
    /// Installs the pgcrypto extension. sp_confirm_booking calls gen_random_bytes(32) to seed
    /// the ticket QrToken; that function lives in pgcrypto. Earlier migrations assumed the
    /// extension was pre-loaded on the host, which isn't true for a vanilla postgres:16 image.
    /// The previous migration (20260417060000) was retroactively edited to include this, but
    /// databases that applied the older version already won't re-run it — hence this explicit
    /// companion migration.
    /// </summary>
    public partial class EnablePgcrypto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Leave the extension in place on rollback — other objects may depend on it.
        }
    }
}
