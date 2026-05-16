using Db.Repositories;
using StackExchange.Redis;

namespace Api.Services;

public class SettingsService(
    IAppSettingRepository repository,
    IConnectionMultiplexer redis
) : ISettingsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private const string CachePrefix = "settings:";

    public async Task<string> GetAsync(string key, CancellationToken ct = default)
    {
        return await GetOrDefaultAsync(key, ct: ct)
            ?? throw new KeyNotFoundException($"Setting '{key}' not found");
    }

    public async Task<string?> GetOrDefaultAsync(string key, string? defaultValue = null, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(CachePrefix + key);
        if (cached.HasValue)
            return cached.ToString();

        var setting = await repository.GetByKeyAsync(key, ct);
        if (setting is null)
            return defaultValue;

        await db.StringSetAsync(CachePrefix + key, setting.Value, CacheTtl);
        return setting.Value;
    }

    public async Task SetAsync(string key, string value, string? description = null, CancellationToken ct = default)
    {
        await repository.UpsertAsync(key, value, description, ct);

        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(CachePrefix + key);
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var settings = await repository.GetAllAsync(ct);
        var result = new Dictionary<string, string>();
        foreach (var setting in settings)
        {
            result[setting.Key] = setting.Value;
        }
        return result;
    }
}
