using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSecretsToEnvVars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EncryptedValue",
                table: "app_settings",
                newName: "Value");

            // Remove secret keys from app_settings — they now live in environment variables
            migrationBuilder.Sql(@"
                DELETE FROM app_settings WHERE ""Key"" IN (
                    'jwt_secret',
                    'stripe_secret_key',
                    'stripe_publishable_key',
                    'stripe_webhook_secret',
                    'resend_api_key',
                    's3_access_key',
                    's3_secret_key',
                    's3_bucket',
                    's3_endpoint_url',
                    'cdn_base_url'
                );
            ");

            // Update the upsert stored procedure to use the renamed column
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION upsert_app_setting(
                    p_key text, p_value text, p_description text DEFAULT NULL
                ) RETURNS void AS $$
                BEGIN
                    INSERT INTO app_settings (""Id"", ""Key"", ""Value"", ""Description"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (gen_random_uuid(), p_key, p_value, p_description, now(), now())
                    ON CONFLICT (""Key"") DO UPDATE SET
                        ""Value"" = EXCLUDED.""Value"",
                        ""Description"" = COALESCE(EXCLUDED.""Description"", app_settings.""Description""),
                        ""UpdatedAt"" = now();
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Value",
                table: "app_settings",
                newName: "EncryptedValue");
        }
    }
}
