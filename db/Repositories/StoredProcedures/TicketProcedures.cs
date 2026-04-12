using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class TicketProcedures(EventPlatformDbContext context) : ITicketProcedures
{
    public async Task InviteTicketAsync(Guid ticketId, string inviteHash, string email, DateTime expiresAt, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_invite_ticket(@p0, @p1, @p2, @p3)",
                [ticketId, inviteHash, email, expiresAt], ct);
    }

    public async Task<TicketClaimResult?> ClaimTicketAsync(string inviteHash, Guid guestUserId, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<TicketClaimResult>(
                "SELECT * FROM sp_claim_ticket(@p0, @p1)",
                inviteHash, guestUserId)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }
}
