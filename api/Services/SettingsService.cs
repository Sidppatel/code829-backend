using Db.Repositories;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// Reads/writes AppSettings with AES-256 encryption and Redis caching (30s TTL).
/// All DB settings are encrypted at rest and only decrypted when read.
/// </summary>
public class SettingsService(
    IAppSettingRepository repository,
    IEncryptionService encryption,
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

        var decrypted = encryption.Decrypt(setting.EncryptedValue);
        await db.StringSetAsync(CachePrefix + key, decrypted, CacheTtl);
        return decrypted;
    }

    public async Task SetAsync(string key, string value, string? description = null)
    {
        var encrypted = encryption.Encrypt(value);
        await repository.UpsertAsync(key, encrypted, description);

        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(CachePrefix + key);
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        var settings = await repository.GetAllAsync();
        var result = new Dictionary<string, string>();
        foreach (var setting in settings)
        {
            result[setting.Key] = encryption.Decrypt(setting.EncryptedValue);
        }
        return result;
    }
}
