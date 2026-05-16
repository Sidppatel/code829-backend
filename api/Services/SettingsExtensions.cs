using Serilog;

namespace Api.Services;

public static class SettingsExtensions
{
    public static async Task<int> GetIntAsync(
    this ISettingsService settings,
    string key,
    int defaultValue,
    int min = 0,
    int max = 1_000_000)
    {
        var raw = await settings.GetOrDefaultAsync(key, defaultValue.ToString());
        if (!int.TryParse(raw, out var value) || value < min || value > max)
        {
            Log.Warning("[Settings] Invalid setting {Key}={Raw}; using default {Default}", key, raw, defaultValue);
            return defaultValue;
        }
        return value;
    }

    public static async Task<bool> GetBoolAsync(
    this ISettingsService settings,
    string key,
    bool defaultValue = false)
    {
        var raw = await settings.GetOrDefaultAsync(key, defaultValue ? "true" : "false");
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        if (bool.TryParse(raw, out var parsed)) return parsed;
        Log.Warning("[Settings] Invalid bool setting {Key}={Raw}; using default {Default}", key, raw, defaultValue);
        return defaultValue;
    }
}
