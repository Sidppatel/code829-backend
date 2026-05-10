using System.Text.RegularExpressions;

namespace Api.Middleware;

/// <summary>
/// Redirects unversioned URLs to /v1/... with a 301. Grace window: 90 days from
/// 2026-04-23 → remove by 2026-07-22 (tracked in BE #16 follow-up issue).
///
/// Paths that already start with /v{digit} or are infra paths (/health, /openapi,
/// /scalar, /swagger, /metrics, static assets) pass through untouched.
/// </summary>
public partial class LegacyApiRedirectMiddleware(RequestDelegate next)
{

    private static readonly string[] PassThroughPrefixes =
    {
        "/health",
        "/openapi",
        "/scalar",
        "/swagger",
        "/metrics",
        "/favicon",
        "/static",
        "/uploads",
        "/webhooks",
        "/sitemap",
        "/robots",
    };

    [GeneratedRegex(@"^/v\d+(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSegmentRegex();

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return next(context);
        }

        if (VersionSegmentRegex().IsMatch(path))
        {
            return next(context);
        }

        foreach (var prefix in PassThroughPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return next(context);
            }
        }

        var target = "/v1" + path + context.Request.QueryString;
        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = target;
        context.Response.Headers["X-Api-Deprecation"] = "unversioned; redirected to /v1";
        return Task.CompletedTask;
    }
}
