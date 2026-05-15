namespace Api.Services;

public interface ISettingsService
{
    Task<string> GetAsync(string key, CancellationToken ct = default);
    Task<string?> GetOrDefaultAsync(string key, string? defaultValue = null, CancellationToken ct = default);
    Task SetAsync(string key, string value, string? description = null, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}
