using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class ForwardedProxyConfigurationTests
{
    [Fact]
    public void TrustsOnlyExplicitlyConfiguredProxyAddresses()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "10.20.30.40"
            })
            .Build();
        var options = new ForwardedHeadersOptions();
        var frameworkDefaults = options.KnownProxies.ToArray();
        var frameworkNetworks = options.KnownIPNetworks.ToArray();

        ForwardedProxyConfiguration.Configure(options, configuration);

        Assert.Equal(ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(new[] { System.Net.IPAddress.Parse("10.20.30.40") }, options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
        Assert.DoesNotContain(System.Net.IPAddress.Loopback, options.KnownProxies);
        Assert.DoesNotContain(System.Net.IPAddress.IPv6Loopback, options.KnownProxies);
        Assert.NotEmpty(frameworkDefaults);
        Assert.NotEmpty(frameworkNetworks);
    }

    [Fact]
    public void RejectsInvalidConfiguredProxyAddresses()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip-address"
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            ForwardedProxyConfiguration.Configure(new ForwardedHeadersOptions(), configuration));

        Assert.Contains("ForwardedHeaders:KnownProxies", error.Message);
    }
}
