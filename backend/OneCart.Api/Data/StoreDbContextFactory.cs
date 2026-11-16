using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OneCart.Api.Data;

public sealed class StoreDbContextFactory : IDesignTimeDbContextFactory<StoreDbContext>
{
    public StoreDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<StoreDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = StoreConnectionStringConfiguration.GetRequired(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(
            optionsBuilder,
            StoreDatabaseProvider.GetProviderName(configuration["Database:Provider"]),
            connectionString);

        return new StoreDbContext(optionsBuilder.Options);
    }
}
