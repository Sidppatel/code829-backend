using Db.Repositories;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// Reads/writes non-sensitive AppSettings with Redis caching (30s TTL).
/// Sensitive secrets are handled by ISecretsProvider (env vars), not this service.
/// </summary>
public class SettingsService(
    IAppSettingRepository repository,
    IConnectionMultiplexer redis
) : ISettingsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private const string CachePrefix = "settings:";

    public async Task<string> GetAsync(string key)
    {
        return await GetOrDefaultAsync(key)
            ?? throw new KeyNotFoundException($"Setting '{key}' not found");
    }

    public async Task<string?> GetOrDefaultAsync(string key, string? defaultValue = null)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(CachePrefix + key);
        if (cached.HasValue)
            return cached.ToString();

        var setting = await repository.GetByKeyAsync(key);
        if (setting is null)
            return defaultValue;

        await db.StringSetAsync(CachePrefix + key, setting.Value, CacheTtl);
        return setting.Value;
    }

    public async Task SetAsync(string key, string value, string? description = null)
    {
        await repository.UpsertAsync(key, value, description);

        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(CachePrefix + key);
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        var settings = await repository.GetAllAsync();
        var result = new Dictionary<string, string>();
        foreach (var setting in settings)
        {
            result[setting.Key] = setting.Value;
        }
        return result;
    }
}
