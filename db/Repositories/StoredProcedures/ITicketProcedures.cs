namespace Db.Repositories.StoredProcedures;

public record TicketClaimResult(Guid TicketId, Guid PurchaseId);

public record TicketClaimByTokenResult(Guid? TicketId, bool Success, string Message, bool AlreadyByMe);

public record TicketClaimSelfResult(bool Success, string Message);

public interface ITicketProcedures
{
    Task<bool> SetInviteAsync(Guid ticketId, string inviteHash, string email, DateTime expiresAt, CancellationToken ct = default);
    Task RevokeInviteAsync(Guid ticketId, CancellationToken ct = default);
    Task<TicketClaimByTokenResult> ClaimByTokenAsync(string inviteHash, Guid guestUserId, CancellationToken ct = default);
    Task<TicketClaimSelfResult> ClaimSelfAsync(Guid ticketId, Guid userId, CancellationToken ct = default);
}
