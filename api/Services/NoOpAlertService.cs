using Serilog;

namespace Api.Services;

/// <summary>
/// Default <see cref="IAlertService"/> that logs alerts at Error severity using
/// the existing Serilog pipeline. Production replacements (Sentry / Resend /
/// PagerDuty) implement the same interface so callers never change.
/// </summary>
public sealed class NoOpAlertService : IAlertService
{
    public Task RaiseAsync(
        string code,
        string message,
        IDictionary<string, string>? context = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (context is { Count: > 0 })
        {
            var rendered = string.Join(
                ", ",
                context
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}"));
            Log.Error("[ALERT:{Code}] {Message} | context: {Context}", code, message, rendered);
        }
        else
        {
            Log.Error("[ALERT:{Code}] {Message}", code, message);
        }

        return Task.CompletedTask;
    }
}
