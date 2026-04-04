using System.Security.Claims;
using Serilog.Context;

namespace Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous"))
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            await next(context);
        }
    }
}
