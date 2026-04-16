using System.Text.Json;
using StackExchange.Redis;

namespace Api.Middleware;

/// <summary>
/// Checks for Idempotency-Key header on POST/PUT requests.
/// If found, caches the response in Redis and returns it on duplicate requests.
/// </summary>
public class IdempotencyMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly HashSet<string> IdempotentMethods = ["POST", "PUT"];

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (!IdempotentMethods.Contains(method))
        {
            await next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await next(context);
            return;
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
            await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(entry), CacheTtl);
        }

        // Write to original stream
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private record CachedResponse(int StatusCode, string Body);
}
