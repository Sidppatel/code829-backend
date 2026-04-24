using System.Security.Cryptography;
using System.Text;
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
public class RateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis, IWebHostEnvironment env)
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

    private const int CatalogLimit = 60;
    private static readonly TimeSpan CatalogWindow = TimeSpan.FromMinutes(1);

    // BE #68 — Per-token magic-link verify counter. Prevents replay / brute-force on a
    // single token. Window covers magic-link expiry + retry grace. Limit = 1: one
    // attempt per token; anything after either a success (AuthService deletes the key)
    // or a failure (key lingers to block retries) is rejected.
    private const int MagicLinkVerifyLimit = 1;
    private static readonly TimeSpan MagicLinkVerifyWindow = TimeSpan.FromMinutes(30);
    private const string MagicLinkVerifyPath = "/auth/magic-link/verify";

    // magic-link request has its own per-email rate limit in AuthController;
    // verify endpoint gets the stricter auth limit here too
    private static readonly string[] AuthPaths = ["/auth/dev-login", "/auth/magic-link/verify", "/admin/auth/login"];
    private static readonly string[] CatalogPaths = ["/events", "/admin/events", "/developer/events", "/events/facets", "/events/schema-list"];
    private static readonly string[] SeatHoldPaths = ["/seats/hold", "/seats/hold-table", "/tables/lock"];
    // Purchase-critical mutations — payment integrity endpoints get their own bucket.
    private static readonly string[] PurchasePaths = ["/purchases", "/purchases/quote"];
    private static readonly string[] ConfirmPathSuffixes = ["/confirm", "/confirm-by-intent"];
    private static readonly string[] BeaconPaths = ["/purchases/cancel-beacon", "/tables/release-beacon"];

    public async Task InvokeAsync(HttpContext context, ISettingsService settings)
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
        // Strip API version segment (/v1, /v2, …) so bucket matching stays stable
        // across versions. BE #16 introduced URL-segment versioning; bucket keys
        // below are defined without the version prefix.
        path = System.Text.RegularExpressions.Regex.Replace(path, @"^/v\d+(?=/|$)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (string.IsNullOrEmpty(path)) path = "/";

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

            await WriteTooManyRequestsAsync(context, retryAfterSeconds);
            return;
        }

        // BE #68 — per-token rate limit for magic-link verify. Runs after the per-IP
        // check so IP-wide abuse still short-circuits first. Reads the token from the
        // JSON body, hashes it, and increments a counter keyed by hash. Single use:
        // AuthService.VerifyMagicLinkAsync deletes the key on successful consume.
        if (string.Equals(path, MagicLinkVerifyPath, StringComparison.OrdinalIgnoreCase))
        {
            var token = await TryReadTokenFromBodyAsync(context);
            if (!string.IsNullOrEmpty(token))
            {
                var tokenHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
                var mlvKey = $"ratelimit:mlv:{tokenHash}";
                var mlvCount = await db.StringIncrementAsync(mlvKey);
                if (mlvCount == 1)
                    await db.KeyExpireAsync(mlvKey, MagicLinkVerifyWindow);

                if (mlvCount > MagicLinkVerifyLimit)
                {
                    var mlvTtl = await db.KeyTimeToLiveAsync(mlvKey);
                    var retryAfterSeconds = (int)Math.Ceiling((mlvTtl ?? MagicLinkVerifyWindow).TotalSeconds);
                    await WriteTooManyRequestsAsync(context, retryAfterSeconds);
                    return;
                }
            }
        }

        await next(context);
    }

    private static async Task WriteTooManyRequestsAsync(HttpContext context, int retryAfterSeconds)
    {
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
    }

    private static async Task<string?> TryReadTokenFromBodyAsync(HttpContext context)
    {
        if (context.Request.ContentLength is null or 0) return null;
        try
        {
            context.Request.EnableBuffering();
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(
                context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var json = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("token", out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
            return null;
        }
        catch (JsonException) { return null; }
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

        if (IsCatalogPath(path))
            return (CatalogLimit, CatalogWindow);

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

        if (IsCatalogPath(path))
            return "catalog";

        return "general";
    }

    private static bool IsPurchasePath(string path) =>
        PurchasePaths.Any(p => path == p || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
        || ConfirmPathSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static bool IsCatalogPath(string path) =>
        CatalogPaths.Any(p => path == p || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
}
