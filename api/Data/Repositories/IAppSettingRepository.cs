using Db.Entities;

namespace Db.Repositories;

public interface IAppSettingRepository
{
    Task<AppSetting?> GetByKeyAsync(string key);
    Task<List<AppSetting>> GetAllAsync();
    Task UpsertAsync(string key, string value, string? description = null);
}
