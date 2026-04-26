using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Enforces one-ticket-per-user-per-purchase at the SP layer.
    /// sp_claim_ticket_self now returns (success, message) and rejects when the same
    /// user already owns another claimed/checked-in ticket on the purchase.
    /// sp_claim_ticket_by_token gets the same guard for token-based claims.
    /// </remarks>
    public partial class EnforceOneTicketPerUserPerPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_claim_ticket_self(uuid, uuid);");
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_claim_ticket_self.sql"));
            migrationBuilder.Sql(MigrationSqlLoader.Load("sp_claim_ticket_by_token.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_claim_ticket_self(uuid, uuid);");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_claim_ticket_self(
    p_ticket_id uuid, p_user_id uuid
) RETURNS boolean LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_updated int;
BEGIN
    UPDATE purchase_tickets SET
        ""GuestUserId"" = p_user_id,
        ""Status"" = 'Claimed',
        ""ClaimedAt"" = now(),
        ""InviteTokenHash"" = NULL,
        ""InviteExpiresAt"" = NULL,
        ""InvitedEmail"" = NULL,
        ""InviteSentAt"" = NULL,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = p_ticket_id
      AND ""Status"" IN ('Unassigned', 'Invited');
    GET DIAGNOSTICS v_updated = ROW_COUNT;
    RETURN v_updated > 0;
END; $$;
");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION sp_claim_ticket_by_token(
    p_invite_hash text, p_guest_user_id uuid
)
RETURNS TABLE(""TicketId"" uuid, ""Success"" boolean, ""Message"" text, ""AlreadyByMe"" boolean)
LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE
    v_id uuid;
    v_status text;
    v_guest_user_id uuid;
    v_expires_at timestamptz;
BEGIN
    SELECT ""Id"", ""Status""::text, ""GuestUserId"", ""InviteExpiresAt""
        INTO v_id, v_status, v_guest_user_id, v_expires_at
        FROM purchase_tickets
        WHERE ""InviteTokenHash"" = p_invite_hash
        FOR UPDATE;

    IF v_id IS NULL THEN
        RETURN QUERY SELECT NULL::uuid, false, 'Invalid or expired invite link', false;
        RETURN;
    END IF;

    IF v_expires_at IS NOT NULL AND v_expires_at < now() THEN
        RETURN QUERY SELECT v_id, false, 'This invite link has expired', false;
        RETURN;
    END IF;

    IF v_status = 'CheckedIn' THEN
        RETURN QUERY SELECT v_id, false, 'This ticket has already been used', false;
        RETURN;
    END IF;

    IF v_guest_user_id = p_guest_user_id THEN
        RETURN QUERY SELECT v_id, true, 'You have already claimed this ticket', true;
        RETURN;
    END IF;

    IF v_status = 'Claimed' THEN
        RETURN QUERY SELECT v_id, false, 'This ticket has already been claimed', false;
        RETURN;
    END IF;

    UPDATE purchase_tickets SET
        ""GuestUserId"" = p_guest_user_id,
        ""ClaimedAt"" = now(),
        ""Status"" = 'Claimed',
        ""InviteTokenHash"" = NULL,
        ""UpdatedAt"" = now()
    WHERE ""Id"" = v_id;

    RETURN QUERY SELECT v_id, true, 'Ticket claimed successfully', false;
END; $$;
");
        }
    }
}
