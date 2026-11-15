using Microsoft.Extensions.Configuration;
using OneCart.Api;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class CorsOriginConfigurationTests
{
    [Fact]
    public void DevelopmentAllowsTheViteOriginsOnSupportedPorts()
    {
        var configuration = new ConfigurationBuilder().Build();

        var origins = CorsOriginConfiguration.GetAllowedOrigins(configuration, isDevelopment: true);

        Assert.Contains("http://localhost:5173", origins);
        Assert.Contains("http://127.0.0.1:5173", origins);
        Assert.Contains("http://localhost:5174", origins);
        Assert.Contains("http://127.0.0.1:5174", origins);
        Assert.Contains("http://localhost:5175", origins);
        Assert.Contains("http://127.0.0.1:5175", origins);
    }

    [Fact]
    public void ProductionUsesOnlyConfiguredOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://shop.example.com"
            })
            .Build();

        var origins = CorsOriginConfiguration.GetAllowedOrigins(configuration, isDevelopment: false);

        Assert.Equal(["https://shop.example.com"], origins);
    }
}
