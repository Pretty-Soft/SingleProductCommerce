using Microsoft.Extensions.Configuration;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class StoreConnectionStringConfigurationTests
{
    [Fact]
    public void Reads_connection_string_from_configured_secret_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"onecart-connection-{Guid.NewGuid():N}.txt");
        const string expected = "Host=database;Database=onecart;Username=store;Password=test-only";
        File.WriteAllText(path, $" {expected} \r\n");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:StoreFile"] = path,
                    ["ConnectionStrings:Store"] = "Host=ignored;Database=ignored"
                })
                .Build();

            Assert.Equal(expected, StoreConnectionStringConfiguration.GetRequired(configuration));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Fails_closed_when_configured_secret_file_is_missing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:StoreFile"] = missingPath,
                ["ConnectionStrings:Store"] = "Host=database;Database=onecart"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => StoreConnectionStringConfiguration.GetRequired(configuration));

        Assert.DoesNotContain("Host=database", exception.Message, StringComparison.Ordinal);
    }
}
