namespace Db.Repositories.StoredProcedures;

public record TicketClaimResult(Guid TicketId, Guid PurchaseId);

public interface ITicketProcedures
{
    Task InviteTicketAsync(Guid ticketId, string inviteHash, string email, DateTime expiresAt, CancellationToken ct = default);
    Task<TicketClaimResult?> ClaimTicketAsync(string inviteHash, Guid guestUserId, CancellationToken ct = default);
}
