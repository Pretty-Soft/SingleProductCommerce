using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OneCart.Api.Admin;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class FirstRunInstallationTests
{
    [Fact]
    public void Builds_provider_specific_credentials_without_string_interpolation()
    {
        var request = new DatabaseSetupRequest("PostgreSql", "localhost", 5432, "onecart", "owner", "p;word=with delimiters");
        Assert.True(FirstRunInstallationService.TryBuildDatabase(request, out var database, out var error), error);
        Assert.Equal(StoreDatabaseProvider.PostgreSql, database!.Provider);
        Assert.Equal("p;word=with delimiters", new Npgsql.NpgsqlConnectionStringBuilder(database.ConnectionString).Password);

        var sql = request with { Provider = "SqlServer", Host = @"localhost\SQLEXPRESS", Port = null };
        Assert.True(FirstRunInstallationService.TryBuildDatabase(sql, out database, out error), error);
        Assert.Equal(StoreDatabaseProvider.SqlServer, database!.Provider);
        Assert.Equal("p;word=with delimiters", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(database.ConnectionString).Password);
        Assert.False(FirstRunInstallationService.TryBuildDatabase(request with { Provider = "Unknown" }, out _, out _));
    }

    [Fact]
    public void Saves_installation_credentials_protected_outside_source_configuration()
    {
        var folder = Path.Combine(Path.GetTempPath(), "onecart-install-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var environment = new TestEnvironment(folder);
            var protection = new EphemeralDataProtectionProvider();
            var installation = new InstallationConfigurationStore(configuration, environment, protection);
            Assert.Null(installation.Current);

            installation.Save(new InstallationDatabase("PostgreSql", "Host=localhost;Password=private-test-value"));
            var file = File.ReadAllText(Path.Combine(folder, "App_Data", "installation.json"));
            Assert.DoesNotContain("private-test-value", file);
            var reloaded = new InstallationConfigurationStore(configuration, environment, protection);
            Assert.Contains("private-test-value", reloaded.Current!.ConnectionString);
            Assert.Throws<InvalidOperationException>(() => reloaded.Save(new InstallationDatabase("SqlServer", "x")));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "OneCart.Api.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
