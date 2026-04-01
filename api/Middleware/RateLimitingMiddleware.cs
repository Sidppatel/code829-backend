using System.Text.Json;
using StackExchange.Redis;

namespace Api.Middleware;

/// <summary>
/// Redis-based rate limiting middleware for distributed deployments.
/// Default: 100 requests per 15 minutes for general endpoints.
/// Stricter limits for auth (5/min) and seat hold (20/min) endpoints.
/// </summary>
public class RateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
{
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

        var ip = remoteIp?.ToString() ?? "unknown";
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";

        var (limit, window) = GetLimitForPath(path);
        var bucket = GetBucketName(path);
        var key = $"ratelimit:{bucket}:{ip}";

        var db = redis.GetDatabase();
        var count = await db.StringIncrementAsync(key);

        if (count == 1)
        {
            await db.KeyExpireAsync(key, window);
        }

        if (count > limit)
        {
            var ttl = await db.KeyTimeToLiveAsync(key);
            var retryAfterSeconds = (int)Math.Ceiling((ttl ?? window).TotalSeconds);

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
}
