using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class CleanupUnusedSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove unused settings keys
            migrationBuilder.Sql("""
                DELETE FROM app_settings WHERE "Key" IN (
                    'brand_tagline', 'brand_primary_color', 'brand_accent_color',
                    'default_theme', 'default_city', 'default_state', 'default_timezone',
                    'smtp_host', 'smtp_port', 'smtp_username', 'smtp_password',
                    'platform_fee_percent', 'platform_fee_flat_cents', 'max_tickets_per_booking'
                );
                """);

            // Rename brand_name → app_name
            migrationBuilder.Sql("""
                UPDATE app_settings
                SET "Key" = 'app_name',
                    "Description" = 'Application name used in emails and SEO'
                WHERE "Key" = 'brand_name';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename app_name back to brand_name
            migrationBuilder.Sql("""
                UPDATE app_settings
                SET "Key" = 'brand_name',
                    "Description" = 'White-label brand name'
                WHERE "Key" = 'app_name';
                """);
        }
    }
}
