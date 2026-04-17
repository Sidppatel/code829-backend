using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

namespace Api.Middleware;

/// <summary>
/// Configures <see cref="ForwardedHeadersOptions"/> so rate limiting and audit logs see the
/// real client IP behind a trusted proxy/CDN, while rejecting spoofed X-Forwarded-For from
/// untrusted peers. Trusted proxies are supplied via the TRUSTED_PROXIES env var as a
/// comma-separated list of CIDRs or plain IPs (e.g. "10.0.0.0/8,1.2.3.4").
/// </summary>
public static class ForwardedHeadersConfig
{
    public static void Configure(ForwardedHeadersOptions options, bool isDevelopment, string? trustedProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 2;

        if (isDevelopment)
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            return;
        }

        if (string.IsNullOrWhiteSpace(trustedProxies))
        {
            Log.Warning(
                "[ForwardedHeaders] TRUSTED_PROXIES unset in non-Development environment. " +
                "Client IPs will collapse to the proxy IP for rate limiting. " +
                "Set TRUSTED_PROXIES to the LB/CDN CIDRs (e.g. \"10.0.0.0/8,172.16.0.0/12\").");
            return;
        }

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var entry in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/'))
            {
                if (System.Net.IPNetwork.TryParse(entry, out var network))
                    options.KnownIPNetworks.Add(network);
                else
                    Log.Warning("[ForwardedHeaders] Invalid CIDR ignored: {Entry}", entry);
            }
            else
            {
                if (IPAddress.TryParse(entry, out var ip))
                    options.KnownProxies.Add(ip);
                else
                    Log.Warning("[ForwardedHeaders] Invalid IP ignored: {Entry}", entry);
            }
        }
    }
}
