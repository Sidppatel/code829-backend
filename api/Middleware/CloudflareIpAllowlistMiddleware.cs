using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Middleware;

/// <summary>
/// Rejects direct-to-origin traffic in Production: only peers whose TCP source
/// IP falls within the Cloudflare IPv4 (<c>TRUSTED_PROXIES</c>) or IPv6
/// (<c>CLOUDFLARE_IPV6_CIDRS</c>) allowlist are permitted. The <c>/health</c>
/// probe path is exempt so Render's readiness check still reaches the service.
///
/// This middleware MUST be registered before <see cref="Microsoft.AspNetCore.Builder.ForwardedHeadersExtensions.UseForwardedHeaders(Microsoft.AspNetCore.Builder.IApplicationBuilder)"/>
/// so the check runs against the raw TCP peer (<see cref="ConnectionInfo.RemoteIpAddress"/>)
/// rather than the rewritten <c>X-Forwarded-For</c> value.
///
/// Enforcement is gated by the <c>CF_IP_ALLOWLIST_ENFORCE</c> env var. The gate
/// exists because the Render → container peer is Render's internal load
/// balancer by default, not Cloudflare. Flip the gate to <c>true</c> only once
/// the Render service is reachable exclusively through a Cloudflare-fronted
/// custom domain (and the Cloudflare Tunnel / origin-pull wiring is verified).
/// </summary>
public sealed class CloudflareIpAllowlistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CloudflareIpAllowlistMiddleware> _logger;
    private readonly bool _active;
    private readonly List<IPNetwork> _allowedNetworks = new();

    public CloudflareIpAllowlistMiddleware(
        RequestDelegate next,
        ILogger<CloudflareIpAllowlistMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;

        var enforce = string.Equals(
            Environment.GetEnvironmentVariable("CF_IP_ALLOWLIST_ENFORCE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!env.IsProduction() || !enforce)
        {
            _active = false;
            return;
        }

        ParseCidrs(Environment.GetEnvironmentVariable("TRUSTED_PROXIES"));
        ParseCidrs(Environment.GetEnvironmentVariable("CLOUDFLARE_IPV6_CIDRS"));

        if (_allowedNetworks.Count == 0)
        {
            _logger.LogError(
                "[CF-allowlist] CF_IP_ALLOWLIST_ENFORCE=true but neither TRUSTED_PROXIES nor CLOUDFLARE_IPV6_CIDRS are set. Refusing to enforce an empty allowlist (would deny all traffic).");
            _active = false;
            return;
        }

        _active = true;
        _logger.LogInformation(
            "[CF-allowlist] active — {Count} CIDR(s) permitted, non-CF peers rejected with 403",
            _allowedNetworks.Count);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_active)
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var peer = context.Connection.RemoteIpAddress;
        if (peer is not null && IsAllowed(peer))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "[CF-allowlist] rejecting direct-to-origin peer {Peer} path={Path}",
            peer?.ToString() ?? "(null)",
            context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Forbidden");
    }

    private bool IsAllowed(IPAddress peer)
    {
        var candidate = peer;
        if (peer.IsIPv4MappedToIPv6)
            candidate = peer.MapToIPv4();

        foreach (var network in _allowedNetworks)
        {
            if (network.BaseAddress.AddressFamily == candidate.AddressFamily
                && network.Contains(candidate))
            {
                return true;
            }
        }
        return false;
    }

    private void ParseCidrs(string? cidrs)
    {
        if (string.IsNullOrWhiteSpace(cidrs)) return;

        foreach (var entry in cidrs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPNetwork.TryParse(entry, out var network))
            {
                _allowedNetworks.Add(network);
            }
            else
            {
                _logger.LogWarning("[CF-allowlist] invalid CIDR ignored: {Entry}", entry);
            }
        }
    }
}
