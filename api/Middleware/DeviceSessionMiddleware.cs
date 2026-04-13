using System.Security.Cryptography;
using System.Text;
using Api.Services;
using Db;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

namespace Api.Middleware;

public class DeviceSessionMiddleware(RequestDelegate next)
{
    private const string CookieName = "session";
    private static readonly TimeSpan JwtCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActivityDebounce = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        HttpContext httpContext,
        EventPlatformDbContext dbContext,
        IJwtService jwtService,
        IConnectionMultiplexer redis)
    {
        var sessionToken = httpContext.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(sessionToken))
        {
            await next(httpContext);
            return;
        }

        var sessionHash = HashToken(sessionToken);
        var db = redis.GetDatabase();
        var cacheKey = $"session:{sessionHash}";

        // Try Redis cache first
        var cachedJwt = await db.StringGetAsync(cacheKey);
        if (cachedJwt.HasValue)
        {
            httpContext.Request.Headers.Authorization = $"Bearer {cachedJwt}";
            await next(httpContext);
            return;
        }

        // Cache miss — validate session in DB
        var session = await dbContext.DeviceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.SessionHash == sessionHash &&
                s.RevokedAt == null &&
                s.ExpiresAt > DateTime.UtcNow);

        if (session is null)
        {
            // Invalid session — clear the cookie
            httpContext.Response.Cookies.Delete(CookieName);
            await next(httpContext);
            return;
        }

        // Resolve user or admin user based on session type
        string? jwt = null;

        if (session.UserId.HasValue)
        {
            var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId);
            if (user is null || !user.IsActive)
            {
                httpContext.Response.Cookies.Delete(CookieName);
                await next(httpContext);
                return;
            }
            jwt = await jwtService.GenerateUserJwtAsync(user);
        }
        else if (session.AdminUserId.HasValue)
        {
            var admin = await dbContext.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == session.AdminUserId);
            if (admin is null || !admin.IsActive)
            {
                httpContext.Response.Cookies.Delete(CookieName);
                await next(httpContext);
                return;
            }
            jwt = await jwtService.GenerateAdminJwtAsync(admin);
        }

        if (jwt is null)
        {
            httpContext.Response.Cookies.Delete(CookieName);
            await next(httpContext);
            return;
        }

        // Cache in Redis
        await db.StringSetAsync(cacheKey, jwt, JwtCacheTtl);

        // Attach to request
        httpContext.Request.Headers.Authorization = $"Bearer {jwt}";

        // Debounced activity update
        if (session.LastActivityAt < DateTime.UtcNow - ActivityDebounce)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await db.StringSetAsync($"session:activity:{sessionHash}", "1", ActivityDebounce);
                    // The actual DB update is deferred — we only update if not recently updated
                    using var scope = httpContext.RequestServices.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<EventPlatformDbContext>();
                    await ctx.Database.ExecuteSqlRawAsync(
                        "SELECT sp_update_session_activity(@p0)", sessionHash);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[Session] Failed to update activity for session");
                }
            });
        }

        await next(httpContext);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
