using Db.Entities;

namespace Db.Repositories.StoredProcedures;

public record UserCounts(int Total, int Active, int NewThisMonth);

public interface IUserProcedures
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByEmailHashAsync(string emailHash, CancellationToken ct = default);
    Task<List<User>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<UserCounts> GetCountsAsync(CancellationToken ct = default);
    Task UpdateUserProfileAsync(Guid userId, string? firstName, string? lastName, string? phone, string? address, string? city, string? state, string? zip, bool? optIn, CancellationToken ct = default);
    Task<Guid?> SetUserImageAsync(Guid userId, Guid imageId, CancellationToken ct = default);
    Task<Guid?> ClearUserImageAsync(Guid userId, CancellationToken ct = default);
    Task<bool> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
