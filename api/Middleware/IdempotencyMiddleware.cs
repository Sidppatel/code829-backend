using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;
using StackExchange.Redis;

namespace Api.Middleware;

/// <summary>
/// Idempotency for POST/PUT. Client-supplied Idempotency-Key is honored when present.
/// Purchase-critical paths (POST /purchases, POST /purchases/{id}/confirm*) also get a
/// server-generated fallback key derived from userId + path + body hash so double-tap
/// without a client key can't create duplicate purchases.
/// </summary>
public class IdempotencyMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FallbackTtl = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> IdempotentMethods = ["POST", "PUT"];

    // Purchase-critical paths where we insist on idempotency even if the client forgot a header.
    private static readonly string[] EnforcedPathPrefixes = ["/purchases"];
    private static readonly string[] EnforcedPathSuffixes = ["/confirm", "/confirm-by-intent"];
    // Pure read-style POSTs under /purchases that never mutate state — skip enforcement.
    private static readonly string[] ExemptPaths = ["/purchases/quote"];

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (!IdempotentMethods.Contains(method))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var isEnforced = IsEnforcedPath(path);

        if (string.IsNullOrEmpty(idempotencyKey))
        {
            if (!isEnforced)
            {
                await next(context);
                return;
            }

            // Fallback: hash the user + path + body. Short TTL — guards against accidental
            // double-tap within minutes, but doesn't pin results for hours.
            idempotencyKey = await BuildFallbackKeyAsync(context, path);
            Log.Warning("[Idempotency] No Idempotency-Key on {Method} {Path} — using server-generated fallback", method, path);
        }

        var cacheKey = $"idempotency:{idempotencyKey}";
        var db = redis.GetDatabase();

        // Check if we already processed this key
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var entry = JsonSerializer.Deserialize<CachedResponse>(cached.ToString());
            if (entry is not null)
            {
                context.Response.StatusCode = entry.StatusCode;
                context.Response.ContentType = "application/json";
                context.Response.Headers["X-Idempotency-Replayed"] = "true";
                await context.Response.WriteAsync(entry.Body);
                return;
            }
        }

        // Capture the response
        var originalBody = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await next(context);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

        // Cache successful responses (2xx)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            var entry = new CachedResponse(context.Response.StatusCode, responseBody);
            var ttl = idempotencyKey!.StartsWith("auto:") ? FallbackTtl : CacheTtl;
            await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(entry), ttl);
        }

        // Write to original stream
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private static bool IsEnforcedPath(string path)
    {
        var p = path.ToLowerInvariant();
        if (ExemptPaths.Any(exempt => p == exempt)) return false;
        if (EnforcedPathSuffixes.Any(suffix => p.EndsWith(suffix))) return true;
        return EnforcedPathPrefixes.Any(prefix =>
            p == prefix || p.StartsWith(prefix + "/") || p.StartsWith(prefix + "?"));
    }

    private static async Task<string> BuildFallbackKeyAsync(HttpContext context, string path)
    {
        context.Request.EnableBuffering();
        context.Request.Body.Position = 0;
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon";
        var payload = $"{userId}|{context.Request.Method}|{path}|{body}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return $"auto:{hash}";
    }

    private record CachedResponse(int StatusCode, string Body);
}
