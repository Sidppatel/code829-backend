using Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace Db.Repositories;

public class AppSettingRepository(EventPlatformDbContext context) : IAppSettingRepository
{
    public async Task<AppSetting?> GetByKeyAsync(string key)
        => await context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);

    public async Task<List<AppSetting>> GetAllAsync()
        => await context.AppSettings.OrderBy(s => s.Key).ToListAsync();

    public async Task UpsertAsync(string key, string encryptedValue, string? description = null)
    {
        var existing = await GetByKeyAsync(key);
        if (existing is not null)
        {
            existing.EncryptedValue = encryptedValue;
            existing.UpdatedAt = DateTime.UtcNow;
            if (description is not null)
                existing.Description = description;
        }
        else
        {
            context.AppSettings.Add(new AppSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                EncryptedValue = encryptedValue,
                Description = description
            });
        }
        await context.SaveChangesAsync();
    }
}
