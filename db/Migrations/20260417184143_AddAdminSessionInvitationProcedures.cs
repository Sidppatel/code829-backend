using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSessionInvitationProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Views ─────────────────────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_admin_users AS
SELECT
    ""Id"", ""Email"", ""EmailHash"", ""FirstName"", ""LastName"",
    ""Role"", ""IsActive"", ""LastLoginAt"", ""AvatarPath"", ""Phone"",
    ""StripeConnectedAccountId"", ""CreatedAt"", ""UpdatedAt""
FROM admin_users;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_device_sessions AS
SELECT
    ""Id"", ""UserId"", ""AdminUserId"", ""SessionHash"",
    ""DeviceFingerprint"", ""DeviceName"", ""IpAddress"",
    ""LastActivityAt"", ""ExpiresAt"", ""RevokedAt"",
    ""CreatedAt"", ""UpdatedAt""
FROM device_sessions;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW v_invitations AS
SELECT
    i.""Id"", i.""Email"", i.""TokenHash"", i.""Role"",
    i.""InvitedByAdminUserId"", i.""Status"",
    i.""ExpiresAt"", i.""AcceptedAt"",
    i.""CreatedAt"", i.""UpdatedAt"",
    a.""FirstName"" AS ""InviterFirstName"",
    a.""LastName"" AS ""InviterLastName""
FROM invitations i
JOIN admin_users a ON i.""InvitedByAdminUserId"" = a.""Id"";
");

            // ─── AdminUser read functions ──────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_admin_by_id(p_id uuid)
RETURNS SETOF admin_users
LANGUAGE sql STABLE AS $$
    SELECT * FROM admin_users WHERE ""Id"" = p_id;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_admin_by_email(p_email text)
RETURNS SETOF admin_users
LANGUAGE sql STABLE AS $$
    SELECT * FROM admin_users WHERE ""Email"" = p_email;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_admin_exists_by_email(p_email text)
RETURNS boolean
LANGUAGE sql STABLE AS $$
    SELECT EXISTS(SELECT 1 FROM admin_users WHERE ""Email"" = p_email);
$$;
");

            // ─── AdminUser lockout procedures ──────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_increment_admin_failed_login(
    p_id uuid, p_max_attempts int, p_lockout_minutes int
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET
        ""FailedLoginAttempts"" = ""FailedLoginAttempts"" + 1,
        ""LockedUntil"" = CASE
            WHEN ""FailedLoginAttempts"" + 1 >= p_max_attempts
                THEN now() + (p_lockout_minutes::text || ' minutes')::interval
            ELSE ""LockedUntil""
        END,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_reset_admin_lockout(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE admin_users SET
        ""FailedLoginAttempts"" = 0,
        ""LockedUntil"" = NULL,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            // ─── Invitation procedures ─────────────────────────────────

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_create_invitation(
    p_email text, p_token_hash text, p_role text,
    p_invited_by uuid, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_id uuid;
BEGIN
    INSERT INTO invitations (""Id"", ""Email"", ""TokenHash"", ""Role"",
        ""InvitedByAdminUserId"", ""Status"", ""ExpiresAt"", ""CreatedAt"", ""UpdatedAt"")
    VALUES (gen_random_uuid(), p_email, p_token_hash, p_role,
        p_invited_by, 'Pending', p_expires_at, now(), now())
    RETURNING ""Id"" INTO v_id;
    RETURN v_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_pending_invitation_by_email(p_email text)
RETURNS SETOF invitations
LANGUAGE sql STABLE AS $$
    SELECT * FROM invitations
    WHERE ""Email"" = p_email
      AND ""Status"" = 'Pending'
      AND ""ExpiresAt"" > now()
    LIMIT 1;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_get_invitation_by_token_hash(p_token_hash text)
RETURNS SETOF invitations
LANGUAGE sql STABLE AS $$
    SELECT * FROM invitations
    WHERE ""TokenHash"" = p_token_hash
      AND ""Status"" = 'Pending'
      AND ""ExpiresAt"" > now()
    LIMIT 1;
$$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_accept_invitation(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE invitations
    SET ""Status"" = 'Accepted',
        ""AcceptedAt"" = now(),
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id;
END; $$;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_revoke_invitation(p_id uuid)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE invitations
    SET ""Status"" = 'Revoked',
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_id AND ""Status"" = 'Pending';
END; $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_revoke_invitation(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_accept_invitation(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_invitation_by_token_hash(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_pending_invitation_by_email(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_create_invitation(text, text, text, uuid, timestamptz);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_reset_admin_lockout(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_increment_admin_failed_login(uuid, int, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_admin_exists_by_email(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_admin_by_email(text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_get_admin_by_id(uuid);");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_invitations;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_device_sessions;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_admin_users;");
        }
    }
}
