using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReadProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // sp_get_user_by_id — returns a single User row by primary key
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_user_by_id(p_user_id uuid)
RETURNS SETOF users
LANGUAGE sql STABLE AS $$
    SELECT * FROM users WHERE ""Id"" = p_user_id;
$$;
");

            // sp_get_user_by_email — returns a single User row by email
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_user_by_email(p_email text)
RETURNS SETOF users
LANGUAGE sql STABLE AS $$
    SELECT * FROM users WHERE ""Email"" = p_email;
$$;
");

            // sp_get_user_by_email_hash — returns a single User row by email hash
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_user_by_email_hash(p_email_hash text)
RETURNS SETOF users
LANGUAGE sql STABLE AS $$
    SELECT * FROM users WHERE ""EmailHash"" = p_email_hash;
$$;
");

            // sp_list_users — returns all Users ordered by CreatedAt
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_list_users()
RETURNS SETOF users
LANGUAGE sql STABLE AS $$
    SELECT * FROM users ORDER BY ""CreatedAt"";
$$;
");

            // sp_user_exists_by_email — boolean existence check
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_user_exists_by_email(p_email text)
RETURNS boolean
LANGUAGE sql STABLE AS $$
    SELECT EXISTS(SELECT 1 FROM users WHERE ""Email"" = p_email);
$$;
");

            // sp_user_counts — aggregated dashboard counts
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_user_counts()
RETURNS TABLE(""Total"" integer, ""Active"" integer, ""NewThisMonth"" integer)
LANGUAGE sql STABLE AS $$
    SELECT
        COUNT(*)::integer AS ""Total"",
        COUNT(*) FILTER (WHERE ""IsActive"" = true)::integer AS ""Active"",
        COUNT(*) FILTER (WHERE ""CreatedAt"" >= date_trunc('month', now()))::integer AS ""NewThisMonth""
    FROM users;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_user_counts();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_user_exists_by_email(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_list_users();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_user_by_email_hash(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_user_by_email(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_user_by_id(uuid);");
        }
    }
}
