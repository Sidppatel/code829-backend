using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Services;
using StackExchange.Redis;

namespace Api.Middleware;

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

    private const int MagicLinkVerifyLimit = 1;
    private static readonly TimeSpan MagicLinkVerifyWindow = TimeSpan.FromMinutes(30);
    private const string MagicLinkVerifyPath = "/auth/magic-link/verify";

    private static readonly string[] AuthPaths = ["/auth/dev-login", "/auth/magic-link/verify", "/admin/auth/login"];
    private static readonly string[] CatalogPaths = ["/events", "/admin/events", "/developer/events", "/events/facets", "/events/schema-list"];
    private static readonly string[] SeatHoldPaths = ["/seats/hold", "/seats/hold-table", "/tables/lock"];

    private static readonly string[] PurchasePaths = ["/purchases", "/purchases/quote"];
    private static readonly string[] ConfirmPathSuffixes = ["/confirm", "/confirm-by-intent"];
    private static readonly string[] BeaconPaths = ["/purchases/cancel-beacon", "/tables/release-beacon"];

    public async Task InvokeAsync(HttpContext context, ISettingsService settings)
    {

        var remoteIp = context.Connection.RemoteIpAddress;
        if (env.IsDevelopment() && remoteIp != null && System.Net.IPAddress.IsLoopback(remoteIp))
        {
            await next(context);
            return;
        }

        var disabled = await settings.GetOrDefaultAsync("rate_limit_disabled", "false", context.RequestAborted);
        if (string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var ip = remoteIp?.ToString() ?? "unknown";
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";

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
