namespace Db.Repositories.StoredProcedures;

public record TicketClaimResult(Guid TicketId, Guid PurchaseId);

public record TicketClaimByTokenResult(Guid? TicketId, bool Success, string Message, bool AlreadyByMe);

public interface ITicketProcedures
{
    Task SetInviteAsync(Guid ticketId, string inviteHash, string email, DateTime expiresAt, CancellationToken ct = default);
    Task RevokeInviteAsync(Guid ticketId, CancellationToken ct = default);
    Task<TicketClaimByTokenResult> ClaimByTokenAsync(string inviteHash, Guid guestUserId, CancellationToken ct = default);
    Task ClaimSelfAsync(Guid ticketId, Guid userId, CancellationToken ct = default);
}
