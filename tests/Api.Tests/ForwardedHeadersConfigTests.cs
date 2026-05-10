using System.Net;
using Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Tests;

public class ForwardedHeadersConfigTests
{
    [Fact]
    public void Always_EnablesXForwardedForAndProto()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfig.Configure(options, isDevelopment: false, trustedProxies: null);

        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedFor);
        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(2);
    }

    [Fact]
    public void Development_ClearsKnownLists()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfig.Configure(options, isDevelopment: true, trustedProxies: null);

        options.KnownIPNetworks.Should().BeEmpty();
        options.KnownProxies.Should().BeEmpty();
    }

    [Fact]
    public void Production_WithNoTrustedProxies_LeavesDefaultLoopback()
    {
        var options = new ForwardedHeadersOptions();
        var defaultNetworkCount = options.KnownIPNetworks.Count;

        ForwardedHeadersConfig.Configure(options, isDevelopment: false, trustedProxies: null);

        options.KnownIPNetworks.Count.Should().Be(defaultNetworkCount);
    }

    [Fact]
    public void Production_WithCidrList_PopulatesKnownNetworks()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfig.Configure(options, isDevelopment: false, trustedProxies: "10.0.0.0/8, 172.16.0.0/12");

        options.KnownIPNetworks.Should().HaveCount(2);
        options.KnownProxies.Should().BeEmpty();
    }

    [Fact]
    public void Production_WithIpList_PopulatesKnownProxies()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfig.Configure(options, isDevelopment: false, trustedProxies: "1.2.3.4,5.6.7.8");

        options.KnownProxies.Should().Contain(IPAddress.Parse("1.2.3.4"));
        options.KnownProxies.Should().Contain(IPAddress.Parse("5.6.7.8"));
        options.KnownIPNetworks.Should().BeEmpty();
    }

    [Fact]
    public void Production_WithMixedEntries_PopulatesBoth()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfig.Configure(options, isDevelopment: false, trustedProxies: "10.0.0.0/8,1.2.3.4");

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownProxies.Should().ContainSingle(p => p.Equals(IPAddress.Parse("1.2.3.4")));
    }

    [Fact]
    public void Production_WithGarbageEntries_IgnoresThem()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersConfig.Configure(options, isDevelopment: false, trustedProxies: "not-an-ip, 10.0.0.0/8, also-bad/99");

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownProxies.Should().BeEmpty();
    }
}
