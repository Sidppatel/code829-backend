namespace Api.Services;

public interface ISettingsService
{
    Task<string> GetAsync(string key);
    Task<string?> GetOrDefaultAsync(string key, string? defaultValue = null);
    Task SetAsync(string key, string value, string? description = null);
    Task<Dictionary<string, string>> GetAllAsync();
}
