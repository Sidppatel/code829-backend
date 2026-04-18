namespace Api.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class;
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
    Task InvalidateEventAsync(Guid eventId);
    Task InvalidateTablesAsync(Guid eventId);
    Task InvalidateUserAsync(Guid userId);
    Task InvalidatePurchasesAsync(Guid? eventId = null);
}
