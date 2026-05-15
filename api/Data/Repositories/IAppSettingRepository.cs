using Db.Entities;

namespace Db.Repositories;

public interface IAppSettingRepository
{
    Task<AppSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<List<AppSetting>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(string key, string value, string? description = null, CancellationToken ct = default);
}
