using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public class SecurityHeadersOptions
{
    public bool EnableHstsAndCsp { get; set; } = true;
    public string[] DefaultSrc { get; set; } = ["'self'"];
    public string[] ScriptSrc { get; set; } = ["'self'", "https://js.stripe.com"];
    public string[] StyleSrc { get; set; } = ["'self'", "https://fonts.googleapis.com"];
    public string[] FontSrc { get; set; } = ["'self'", "https://fonts.gstatic.com"];

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

public class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
{

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
