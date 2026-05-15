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
        IBusinessUserProcedures businessUserProc,
        IServiceScopeFactory scopeFactory)
    {

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

        var cachedJwt = await db.StringGetAsync(cacheKey);
        if (cachedJwt.HasValue)
        {
            httpContext.Request.Headers.Authorization = $"Bearer {cachedJwt}";
            await next(httpContext);
            return;
        }

        var session = await dbContext.DeviceSessionViews
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.SessionHash == sessionHash &&
                s.RevokedAt == null &&
                s.ExpiresAt > DateTime.UtcNow, httpContext.RequestAborted);

        if (session is null)
        {

            httpContext.Response.Cookies.Delete(cookieName);
            await next(httpContext);
            return;
        }

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

        string? jwt = null;

        if (session.UserId.HasValue)
        {
            var user = await userProc.GetByIdAsync(session.UserId.Value, httpContext.RequestAborted);
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
            var admin = await businessUserProc.GetByIdAsync(session.BusinessUserId.Value, httpContext.RequestAborted);
            if (admin is null || !admin.IsActive)
            {
                httpContext.Response.Cookies.Delete(cookieName);
                await next(httpContext);
                return;
            }

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

        await db.StringSetAsync(cacheKey, jwt, JwtCacheTtl);

        httpContext.Request.Headers.Authorization = $"Bearer {jwt}";

        if (session.LastActivityAt < DateTime.UtcNow - ActivityDebounce)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await db.StringSetAsync($"session:activity:{sessionHash}", "1", ActivityDebounce);

                    using var scope = scopeFactory.CreateScope();
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
