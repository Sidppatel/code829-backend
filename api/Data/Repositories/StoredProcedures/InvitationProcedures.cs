using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Db.Repositories.StoredProcedures;

public class InvitationProcedures(EventPlatformDbContext context) : IInvitationProcedures
{
    public async Task<Guid> CreateAsync(string email, string tokenHash, string role, Guid invitedBy, DateTime expiresAt, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_invitation(@p0, @p1, @p2, @p3, @p4) AS \"Value\"",
                new NpgsqlParameter("p0", email),
                new NpgsqlParameter("p1", tokenHash),
                new NpgsqlParameter("p2", role),
                new NpgsqlParameter("p3", invitedBy),
                new NpgsqlParameter("p4", expiresAt))
            .FirstAsync(ct);
    }

    public async Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Invitations
            .FromSqlRaw("SELECT * FROM sp_get_pending_invitation_by_email({0})", email)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await context.Invitations
            .FromSqlRaw("SELECT * FROM sp_get_invitation_by_token_hash({0})", tokenHash)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task AcceptAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync("SELECT sp_accept_invitation({0})", [id], ct);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync("SELECT sp_revoke_invitation({0})", [id], ct);
    }
}
