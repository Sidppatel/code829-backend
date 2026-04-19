using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class TicketProcedures(EventPlatformDbContext context) : ITicketProcedures
{
    public async Task SetInviteAsync(Guid ticketId, string inviteHash, string email, DateTime expiresAt, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_set_ticket_invite(@p0, @p1, @p2, @p3)",
            [ticketId, inviteHash, email, expiresAt], ct);
    }

    public async Task RevokeInviteAsync(Guid ticketId, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_revoke_ticket_invite(@p0)",
            [ticketId], ct);
    }

    public async Task<TicketClaimByTokenResult> ClaimByTokenAsync(string inviteHash, Guid guestUserId, CancellationToken ct = default)
    {
        var rows = await context.Database
            .SqlQueryRaw<TicketClaimByTokenRow>(
                "SELECT * FROM sp_claim_ticket_by_token(@p0, @p1)",
                inviteHash, guestUserId)
            .ToListAsync(ct);

        var row = rows.FirstOrDefault();
        return row is null
            ? new TicketClaimByTokenResult(null, false, "Invalid or expired invite link", false)
            : new TicketClaimByTokenResult(row.TicketId, row.Success, row.Message, row.AlreadyByMe);
    }

    public async Task ClaimSelfAsync(Guid ticketId, Guid userId, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_claim_ticket_self(@p0, @p1)",
            [ticketId, userId], ct);
    }

    private sealed class TicketClaimByTokenRow
    {
        public Guid? TicketId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool AlreadyByMe { get; set; }
    }
}
