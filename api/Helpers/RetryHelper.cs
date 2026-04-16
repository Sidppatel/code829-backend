using Serilog;

namespace Api.Helpers;

public static class RetryHelper
{
    public static async Task<T> WithRetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 2,
        int baseDelayMs = 500,
        string? context = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
            {
                var delay = baseDelayMs * (int)Math.Pow(2, attempt);
                Log.Warning("[Retry] {Context} attempt {Attempt} failed, retrying in {Delay}ms: {Error}",
                    context ?? "Operation", attempt + 1, delay, ex.Message);
                await Task.Delay(delay);
            }
        }
    }

    public static async Task WithRetryAsync(
        Func<Task> action,
        int maxRetries = 2,
        int baseDelayMs = 500,
        string? context = null)
    {
        await WithRetryAsync(async () => { await action(); return true; }, maxRetries, baseDelayMs, context);
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException
        || (ex.InnerException is HttpRequestException or TimeoutException);
}
