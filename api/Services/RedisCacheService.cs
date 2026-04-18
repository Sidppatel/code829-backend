using System.Text.Json;
using StackExchange.Redis;

namespace Api.Services;

public class RedisCacheService(IConnectionMultiplexer redis) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions) : null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (ttl.HasValue)
            await _db.StringSetAsync(key, json, ttl.Value);
        else
            await _db.StringSetAsync(key, json);
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        var server = redis.GetServers().FirstOrDefault();
        if (server is null) return;

        var keys = server.Keys(pattern: $"{prefix}*").ToArray();
        if (keys.Length > 0)
            await _db.KeyDeleteAsync(keys);
    }

    public async Task InvalidateEventAsync(Guid eventId)
    {
        await RemoveAsync($"event:{eventId}");
        await RemoveByPrefixAsync("events:list:");
    }

    public async Task InvalidateTablesAsync(Guid eventId)
    {
        await RemoveAsync($"event:{eventId}:tables");
    }

    public async Task InvalidateUserAsync(Guid userId)
    {
        await RemoveAsync($"user:{userId}:profile");
    }

    public async Task InvalidatePurchasesAsync(Guid? eventId = null)
    {
        if (eventId.HasValue)
        {
            await RemoveByPrefixAsync($"purchases:list:{eventId}:");
            await RemoveAsync($"purchases:stats:{eventId}");
        }
        else
        {
            await RemoveByPrefixAsync("purchases:");
        }
    }
}
