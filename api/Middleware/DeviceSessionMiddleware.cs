using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Services;
using Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
        ISettingsService settingsService,
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

        // Load user
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId);
        if (user is null || !user.IsActive)
        {
            httpContext.Response.Cookies.Delete(CookieName);
            await next(httpContext);
            return;
        }

        // Generate fresh JWT
        var jwt = await GenerateJwtAsync(user, settingsService);

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

    private static async Task<string> GenerateJwtAsync(Db.Entities.User user, ISettingsService settingsService)
    {
        var jwtSecret = await settingsService.GetAsync("jwt_secret");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "code829-api",
            audience: "code829-client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
