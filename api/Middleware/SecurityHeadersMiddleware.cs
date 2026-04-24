using System.Security.Cryptography;
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
    public string[] ImgSrc { get; set; } = ["'self'", "data:", "blob:", "https:"];
    public string[] ConnectSrc { get; set; } = ["'self'", "https://api.stripe.com", "https://r.stripe.com"];
    public string[] FrameSrc { get; set; } = ["'self'", "https://js.stripe.com", "https://hooks.stripe.com"];
}

/// <summary>
/// Adds production security headers to all responses:
/// HSTS, X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
{
    // Key under which the per-request CSP nonce is exposed to downstream components.
    // Any server-rendered content that injects inline scripts must read it and set
    // nonce="..." on the script tag. JSON APIs don't need it but the value is still
    // emitted so the header stays consistent across requests.
    public const string NonceHttpContextKey = "csp-nonce";

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

        // BE #95 — per-request CSP nonce. Generated for every request so even error
        // pages and redirects get a fresh value. Stripe.js is allow-listed by origin
        // so adding the nonce does not affect Stripe Elements loading.
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceHttpContextKey] = nonce;

        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (_opts.EnableHstsAndCsp && !env.IsDevelopment())
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            context.Response.Headers["Content-Security-Policy"] = BuildCsp(nonce);
        }

        await next(context);
    }

    private string BuildCsp(string nonce)
    {
        var scriptSrc = new string[_opts.ScriptSrc.Length + 1];
        scriptSrc[0] = $"'nonce-{nonce}'";
        Array.Copy(_opts.ScriptSrc, 0, scriptSrc, 1, _opts.ScriptSrc.Length);

        return string.Join("; ", new[]
        {
            $"default-src {string.Join(' ', _opts.DefaultSrc)}",
            $"script-src {string.Join(' ', scriptSrc)}",
            $"style-src {string.Join(' ', _opts.StyleSrc)}",
            $"font-src {string.Join(' ', _opts.FontSrc)}",
            $"img-src {string.Join(' ', _opts.ImgSrc)}",
            $"connect-src {string.Join(' ', _opts.ConnectSrc)}",
            $"frame-src {string.Join(' ', _opts.FrameSrc)}",
        });
    }
}
