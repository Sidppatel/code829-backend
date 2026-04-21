using System.Security.Cryptography;
using System.Text;
using Api.Helpers;
using Api.Services;
using Db;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

namespace Api.Middleware;

public class DeviceSessionMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan JwtCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActivityDebounce = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        HttpContext httpContext,
        EventPlatformDbContext dbContext,
        IJwtService jwtService,
        IConnectionMultiplexer redis,
        IUserProcedures userProc,
        IBusinessUserProcedures businessUserProc)
    {
        // Per-portal cookie lookup: each frontend declares which portal it is via X-Portal,
        // so we know exactly which cookie to resolve. Missing/unknown header = no session.
        var portal = PortalHelper.ReadPortal(httpContext.Request);
        var cookieName = PortalHelper.CookieFor(portal);
        if (cookieName is null)
        {
            await next(httpContext);
            return;
        }

        var sessionToken = httpContext.Request.Cookies[cookieName];
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
        var session = await dbContext.DeviceSessionViews
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.SessionHash == sessionHash &&
                s.RevokedAt == null &&
                s.ExpiresAt > DateTime.UtcNow);

        if (session is null)
        {
            // Invalid session — clear the cookie
            httpContext.Response.Cookies.Delete(cookieName);
            await next(httpContext);
            return;
        }

        // Portal-vs-session-type guard: a session_user cookie must resolve to a user session,
        // and session_admin / session_staff / session_developer to an admin session. This catches
        // stale cookies after DB reseeds and blocks any cross-portal resolution attempts.
        var isAdminPortal = PortalHelper.IsAdminPortal(portal);
        if (isAdminPortal && !session.BusinessUserId.HasValue)
        {
            httpContext.Response.Cookies.Delete(cookieName);
            await next(httpContext);
            return;
        }
        if (!isAdminPortal && !session.UserId.HasValue)
        {
            httpContext.Response.Cookies.Delete(cookieName);
            await next(httpContext);
            return;
        }

        // Resolve user or admin user based on session type
        string? jwt = null;

        if (session.UserId.HasValue)
        {
            var user = await userProc.GetByIdAsync(session.UserId.Value);
            if (user is null || !user.IsActive)
            {
                httpContext.Response.Cookies.Delete(cookieName);
                await next(httpContext);
                return;
            }
            jwt = await jwtService.GenerateUserJwtAsync(user);
        }
        else if (session.BusinessUserId.HasValue)
        {
            var admin = await businessUserProc.GetByIdAsync(session.BusinessUserId.Value);
            if (admin is null || !admin.IsActive)
            {
                httpContext.Response.Cookies.Delete(cookieName);
                await next(httpContext);
                return;
            }

            // Role-vs-portal guard: staff portal requires ≥Staff, admin ≥Admin, developer =Developer.
            // An admin who downgrades roles between sessions won't keep a stale higher-privilege cookie.
            var minRole = PortalHelper.MinRoleForPortal(portal);
            if (minRole.HasValue && (int)admin.Role < (int)minRole.Value)
            {
                httpContext.Response.Cookies.Delete(cookieName);
                await next(httpContext);
                return;
            }
            jwt = await jwtService.GenerateAdminJwtAsync(admin);
        }

        if (jwt is null)
        {
            httpContext.Response.Cookies.Delete(cookieName);
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
