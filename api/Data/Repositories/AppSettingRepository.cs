using Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace Db.Repositories;

public class AppSettingRepository(EventPlatformDbContext context) : IAppSettingRepository
{
    public async Task<AppSetting?> GetByKeyAsync(string key, CancellationToken ct = default)
        => await context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task<List<AppSetting>> GetAllAsync(CancellationToken ct = default)
        => await context.AppSettings.OrderBy(s => s.Key).ToListAsync(ct);

    public async Task UpsertAsync(string key, string value, string? description = null, CancellationToken ct = default)
    {
        var existing = await GetByKeyAsync(key, ct);
        if (existing is not null)
        {
            existing.Value = value;
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
                Value = value,
                Description = description
            });
        }
        await context.SaveChangesAsync(ct);
    }
}
