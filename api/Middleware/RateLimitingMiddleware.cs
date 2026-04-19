using System.Text.Json;
using Api.Services;
using StackExchange.Redis;

namespace Api.Middleware;

/// <summary>
/// Redis-based rate limiting middleware for distributed deployments.
/// Default: 30 requests per 15 minutes for general endpoints.
/// Stricter limits for auth (5/min) and seat hold (20/min) endpoints.
/// Set AppSetting "rate_limit_disabled" = "true" to bypass all limits (useful for testing).
/// </summary>
public class RateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis, IWebHostEnvironment env, ISettingsService settings)
{
    private const int DefaultLimit = 200;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(15);

    private const int AuthLimit = 5;
    private static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(1);

    private const int SeatHoldLimit = 20;
    private static readonly TimeSpan SeatHoldWindow = TimeSpan.FromMinutes(1);

    private const int PurchaseLimit = 10;
    private static readonly TimeSpan PurchaseWindow = TimeSpan.FromMinutes(1);

    private const int BeaconLimit = 20;
    private static readonly TimeSpan BeaconWindow = TimeSpan.FromMinutes(1);

    // magic-link request has its own per-email rate limit in AuthController;
    // verify endpoint gets the stricter auth limit here too
    private static readonly string[] AuthPaths = ["/auth/dev-login", "/auth/magic-link/verify", "/admin/auth/login"];
    private static readonly string[] SeatHoldPaths = ["/seats/hold", "/seats/hold-table", "/tables/lock"];
    // Purchase-critical mutations — payment integrity endpoints get their own bucket.
    private static readonly string[] PurchasePaths = ["/purchases", "/purchases/quote"];
    private static readonly string[] ConfirmPathSuffixes = ["/confirm", "/confirm-by-intent"];
    private static readonly string[] BeaconPaths = ["/purchases/cancel-beacon", "/tables/release-beacon"];

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for loopback ONLY in Development — in Production we never trust
        // loopback IPs even if they reach the app, so misconfiguration can't disable limits.
        var remoteIp = context.Connection.RemoteIpAddress;
        if (env.IsDevelopment() && remoteIp != null && System.Net.IPAddress.IsLoopback(remoteIp))
        {
            await next(context);
            return;
        }

        // DB flag: set AppSetting "rate_limit_disabled" = "true" to bypass all limits.
        // Cached in Redis for 30s, so changes take effect within half a minute.
        var disabled = await settings.GetOrDefaultAsync("rate_limit_disabled", "false");
        if (string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
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
                statusCode = 429,
                message = "Too many requests. Please try again later.",
                correlationId = context.TraceIdentifier
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

        if (BeaconPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (BeaconLimit, BeaconWindow);

        if (IsPurchasePath(path))
            return (PurchaseLimit, PurchaseWindow);

        if (SeatHoldPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return (SeatHoldLimit, SeatHoldWindow);

        return (DefaultLimit, DefaultWindow);
    }

    private static string GetBucketName(string path)
    {
        if (AuthPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return "auth";

        if (BeaconPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return "beacon";

        if (IsPurchasePath(path))
            return "purchase";

        if (SeatHoldPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return "seat-hold";

        return "general";
    }

    private static bool IsPurchasePath(string path) =>
        PurchasePaths.Any(p => path == p || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
        || ConfirmPathSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
