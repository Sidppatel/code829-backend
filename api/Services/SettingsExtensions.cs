using Serilog;

namespace Api.Services;

public static class SettingsExtensions
{
    /// <summary>
    /// Reads an integer setting, falling back to <paramref name="defaultValue"/> and logging a
    /// warning when the stored value is missing, non-numeric, or outside <paramref name="min"/>..<paramref name="max"/>.
    /// Pricing and quota math stay predictable even if an admin pastes "abc" or a negative fee.
    /// </summary>
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

    /// <summary>
    /// Reads a boolean setting (stored as "true"/"false" string) with safe fallback.
    /// Used by the Stripe Connect enforcement flag and other gradual-rollout toggles.
    /// </summary>
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
