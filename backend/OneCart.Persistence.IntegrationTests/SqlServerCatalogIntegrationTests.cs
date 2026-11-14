using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Persistence.IntegrationTests;

public sealed class SqlServerCatalogIntegrationTests
{
    [Fact]
    public async Task SqlServer_migrates_and_round_trips_a_published_product()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("ONECART_SQLSERVER_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString),
            "Set ONECART_SQLSERVER_TEST_CONNECTION to a SQL Server login allowed to create and drop databases.");
        var configuredConnection = baseConnectionString
            ?? throw new InvalidOperationException("SQL Server integration connection string is required.");

        var databaseName = $"OneCart_Integration_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(configuredConnection)
        {
            InitialCatalog = databaseName
        };
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, StoreDatabaseProvider.SqlServer, connection.ConnectionString);

        await using var database = new StoreDbContext(options.Options);
        try
        {
            await database.Database.MigrateAsync();

            var request = new AdminProductRequest(
                "SQL Server Integration Product",
                "sql-server-integration-product",
                "Product persisted by the SQL Server integration test.",
                true,
                [new AdminProductVariantRequest(
                    null,
                    $"IT-{Guid.NewGuid():N}",
                    "Standard",
                    19.95m,
                    "USD",
                    7,
                    true)],
                [new AdminProductAttributeGroupRequest(
                    null,
                    "Memory",
                    true,
                    false,
                    0,
                    [new AdminProductAttributeOptionRequest(null, "16 GB", 25m, true, 0)])]);

            var writeService = new AdminCatalogService(database);
            var saved = await writeService.SaveProductAsync(
                request,
                Guid.NewGuid(),
                "integration-test@onecart.local",
                CancellationToken.None);

            Assert.Null(saved.Errors);
            Assert.NotNull(saved.Product);

            var readService = new StorefrontCatalogService(database);
            var published = await readService.GetPublishedProductAsync(CancellationToken.None);

            Assert.NotNull(published);
            Assert.Equal("sql-server-integration-product", published.Slug);
            Assert.Single(published.Variants);
            Assert.Equal(19.95m, published.Variants[0].Price);
            Assert.True(published.Variants[0].IsAvailable);
            Assert.Single(published.AttributeGroups);
            Assert.Equal("Memory", published.AttributeGroups[0].Name);
            Assert.Equal(25m, published.AttributeGroups[0].Options[0].PriceAdjustment);
            Assert.Single(await database.AdminAuditEvents.AsNoTracking().ToListAsync());
        }
        finally
        {
            try
            {
                await database.Database.EnsureDeletedAsync();
            }
            catch
            {
                // Preserve the test failure if SQL Server did not create the disposable database.
            }
        }
    }
}
