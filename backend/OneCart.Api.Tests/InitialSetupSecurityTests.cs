using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OneCart.Api.Admin;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class InitialSetupSecurityTests
{
    [Fact]
    public void Requires_matching_high_entropy_setup_token()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Setup:BootstrapToken"] = new string('a', 64)
            })
            .Build();

        Assert.True(InitialSetupSecurity.HasValidToken(configuration, new string('a', 64)));
        Assert.False(InitialSetupSecurity.HasValidToken(configuration, new string('b', 64)));
        Assert.False(InitialSetupSecurity.HasValidToken(configuration, new string('a', 31)));
    }

    [Fact]
    public void Requires_exact_configured_request_origin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://shop.example.com"
            })
            .Build();
        var request = new DefaultHttpContext().Request;
        request.Headers.Origin = "https://shop.example.com";

        Assert.True(InitialSetupSecurity.IsAllowedOrigin(request, configuration, isDevelopment: false));
        request.Headers.Origin = "https://attacker.example";
        Assert.False(InitialSetupSecurity.IsAllowedOrigin(request, configuration, isDevelopment: false));
        request.Headers.Remove("Origin");
        Assert.False(InitialSetupSecurity.IsAllowedOrigin(request, configuration, isDevelopment: false));
    }
}
