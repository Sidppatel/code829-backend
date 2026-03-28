using System.Security.Claims;
using Contracts.Enums;

namespace Api.Middleware;

/// <summary>
/// Attribute to declare the minimum role required for an endpoint.
/// Role hierarchy: Developer > Admin > Staff > User.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireRoleAttribute(UserRole minimumRole) : Attribute
{
    public UserRole MinimumRole { get; } = minimumRole;
}

public class RoleAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var roleAttr = endpoint?.Metadata.GetMetadata<RequireRoleAttribute>();

        if (roleAttr is not null)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { message = "Authentication required" });
                return;
            }

            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim is null || !Enum.TryParse<UserRole>(roleClaim, out var userRole) || userRole < roleAttr.MinimumRole)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { message = "Insufficient permissions" });
                return;
            }
        }

        await next(context);
    }
}
