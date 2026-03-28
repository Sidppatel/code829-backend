using Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace Db.Repositories;

public class UserRepository(EventPlatformDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> GetByEmailAsync(string email)
        => await context.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByEmailHashAsync(string emailHash)
        => await context.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);

    public async Task<List<User>> GetAllAsync()
        => await context.Users.OrderBy(u => u.CreatedAt).ToListAsync();

    public async Task<User> CreateAsync(User user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        context.Users.Update(user);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string email)
        => await context.Users.AnyAsync(u => u.Email == email);
}
