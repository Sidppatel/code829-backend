using System.Collections.Concurrent;
using System.Text.Json;

namespace Api.Middleware;

/// <summary>
/// In-memory rate limiting middleware using ConcurrentDictionary.
/// Default: 100 requests per 15 minutes for general endpoints.
/// Stricter limits for auth (5/min) and seat hold (20/min) endpoints.
/// </summary>
public class RateLimitingMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<string, RequestTracker> Trackers = new();
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private static readonly object CleanupLock = new();

    private const int DefaultLimit = 100;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(15);

    private const int AuthLimit = 5;
    private static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(1);

    private const int SeatHoldLimit = 20;
    private static readonly TimeSpan SeatHoldWindow = TimeSpan.FromMinutes(1);

    private static readonly string[] AuthPaths = ["/auth/magic-link", "/auth/dev-login"];
    private static readonly string[] SeatHoldPaths = ["/seats/hold", "/seats/hold-table"];

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for loopback — React StrictMode double-invokes effects in dev
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null && System.Net.IPAddress.IsLoopback(remoteIp))
        {
            await next(context);
            return;
        }

        CleanupExpiredEntries();

        var ip = remoteIp?.ToString() ?? "unknown";
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";

        var (limit, window) = GetLimitForPath(path);
        var key = $"{ip}:{GetBucketName(path)}";

        var tracker = Trackers.GetOrAdd(key, _ => new RequestTracker(window));

        if (!tracker.TryIncrement(limit, window))
        {
            var retryAfterSeconds = (int)Math.Ceiling(tracker.GetTimeUntilReset(window).TotalSeconds);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            context.Response.ContentType = "application/json";

            var response = JsonSerializer.Serialize(new
            {
                error = "Too many requests. Please try again later.",
                retryAfterSeconds
            });

            await context.Response.WriteAsync(response);
            return;
        }

        await next(context);
    }

    private static (int limit, TimeSpan window) GetLimitForPath(string path)
    {
        if (AuthPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (AuthLimit, AuthWindow);

        if (SeatHoldPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (SeatHoldLimit, SeatHoldWindow);

        return (DefaultLimit, DefaultWindow);
    }

    private static string GetBucketName(string path)
    {
        if (AuthPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return "auth";

        if (SeatHoldPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return "seat-hold";

        return "general";
    }

    private static void CleanupExpiredEntries()
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
            return;

        lock (CleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
                return;

            _lastCleanup = DateTime.UtcNow;

            var expiredKeys = Trackers
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                Trackers.TryRemove(key, out _);
            }
        }
    }

    private sealed class RequestTracker
    {
        private int _count;
        private DateTime _windowStart;
        private readonly object _lock = new();

        public RequestTracker(TimeSpan window)
        {
            _windowStart = DateTime.UtcNow;
            _count = 0;
        }

        public bool TryIncrement(int limit, TimeSpan window)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                if (now - _windowStart >= window)
                {
                    _windowStart = now;
                    _count = 0;
                }

                if (_count >= limit)
                    return false;

                _count++;
                return true;
            }
        }

        public TimeSpan GetTimeUntilReset(TimeSpan window)
        {
            lock (_lock)
            {
                var elapsed = DateTime.UtcNow - _windowStart;
                var remaining = window - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public bool IsExpired()
        {
            lock (_lock)
            {
                // Consider expired if no activity for 30 minutes
                return DateTime.UtcNow - _windowStart > TimeSpan.FromMinutes(30);
            }
        }
    }
}
