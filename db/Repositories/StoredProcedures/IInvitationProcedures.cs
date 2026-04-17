using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public interface IInvitationProcedures
{
    Task<Guid> CreateAsync(string email, string tokenHash, string role, Guid invitedBy, DateTime expiresAt, CancellationToken ct = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default);
    Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task AcceptAsync(Guid id, CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
}
