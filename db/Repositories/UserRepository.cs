using Db.Entities;
using Db.Repositories.StoredProcedures;

namespace Db.Repositories;

public class UserRepository(IUserProcedures userProcedures) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id) => userProcedures.GetByIdAsync(id);
    public Task<User?> GetByEmailAsync(string email) => userProcedures.GetByEmailAsync(email);
    public Task<User?> GetByEmailHashAsync(string emailHash) => userProcedures.GetByEmailHashAsync(emailHash);
}
