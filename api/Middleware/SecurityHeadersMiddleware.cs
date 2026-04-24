using Microsoft.Extensions.Options;

namespace Api.Middleware;

/// <summary>
/// CSP and HSTS configuration. Defaults include Stripe domains required for
/// Stripe.js / Elements to load. Override via appsettings "Security:Csp".
/// </summary>
public class SecurityHeadersOptions
{
    public bool EnableHstsAndCsp { get; set; } = true;
    public string[] DefaultSrc { get; set; } = ["'self'"];
    public string[] ScriptSrc { get; set; } = ["'self'", "https://js.stripe.com"];
    public string[] StyleSrc { get; set; } = ["'self'", "https://fonts.googleapis.com"];
    public string[] FontSrc { get; set; } = ["'self'", "https://fonts.gstatic.com"];
    // Narrow to only the origins the app actually loads images from:
    //   - 'self' + data: + blob: for inline/generated previews (QR codes, upload previews)
    //   - R2 public bucket for user-uploaded event art
    //   - Cloudflare Images delivery for resized variants
    // Override via appsettings "Security:Csp:ImgSrc" if a new CDN is introduced.
    public string[] ImgSrc { get; set; } = [
        "'self'",
        "data:",
        "blob:",
        "https://*.r2.cloudflarestorage.com",
        "https://imagedelivery.net",
    ];
    public string[] ConnectSrc { get; set; } = ["'self'", "https://api.stripe.com", "https://r.stripe.com"];
    public string[] FrameSrc { get; set; } = ["'self'", "https://js.stripe.com", "https://hooks.stripe.com"];
}

/// <summary>
/// Adds production security headers to all responses:
/// HSTS, X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
{
    private readonly SecurityHeadersOptions _opts = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "0";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=()";
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Server");

        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (_opts.EnableHstsAndCsp && !env.IsDevelopment())
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            context.Response.Headers["Content-Security-Policy"] = BuildCsp();
        }

        await next(context);
    }

    private string BuildCsp() =>
        string.Join("; ", new[]
        {
            $"default-src {string.Join(' ', _opts.DefaultSrc)}",
            $"script-src {string.Join(' ', _opts.ScriptSrc)}",
            $"style-src {string.Join(' ', _opts.StyleSrc)}",
            $"font-src {string.Join(' ', _opts.FontSrc)}",
            $"img-src {string.Join(' ', _opts.ImgSrc)}",
            $"connect-src {string.Join(' ', _opts.ConnectSrc)}",
            $"frame-src {string.Join(' ', _opts.FrameSrc)}",
        });
}
