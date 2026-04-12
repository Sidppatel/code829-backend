namespace Db.Repositories.StoredProcedures;

public interface IUserProcedures
{
    Task UpdateUserProfileAsync(Guid userId, string firstName, string lastName, string? phone, string? address, string? city, string? state, string? zip, bool optIn, CancellationToken ct = default);
    Task UpdateUserAvatarAsync(Guid userId, string avatarPath, CancellationToken ct = default);
    Task UpdateUserRoleAsync(Guid userId, string role, CancellationToken ct = default);
}
