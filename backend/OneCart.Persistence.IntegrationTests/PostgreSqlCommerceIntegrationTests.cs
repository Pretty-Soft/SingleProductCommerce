using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;
using Xunit;

namespace OneCart.Persistence.IntegrationTests;

public sealed class PostgreSqlCommerceIntegrationTests
{
    [Fact]
    public async Task PostgreSql_migrates_catalog_and_completes_cod_inventory_workflow()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("ONECART_POSTGRES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configuredConnection),
            "Set ONECART_POSTGRES_TEST_CONNECTION to a PostgreSQL login allowed to create and drop databases.");
        var baseConnectionString = configuredConnection
            ?? throw new InvalidOperationException("PostgreSQL integration connection string is required.");

        var databaseName = $"onecart_it_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, StoreDatabaseProvider.PostgreSql, connection.ConnectionString);

        await using var database = new StoreDbContext(options.Options);
        try
        {
            await database.Database.MigrateAsync();

            var request = new AdminProductRequest(
                "PostgreSQL Integration Product",
                "postgresql-integration-product",
                "Product persisted by the PostgreSQL integration test.",
                true,
                [new AdminProductVariantRequest(null, $"PG-{Guid.NewGuid():N}", "Standard", 19.95m, "USD", 7, true)],
                [new AdminProductAttributeGroupRequest(null, "Memory", true, false, 0,
                    [new AdminProductAttributeOptionRequest(null, "16 GB", 25m, true, 0)])])
            {
                CurrencyCode = "USD"
            };

            var save = await new AdminCatalogService(database).SaveProductAsync(
                request,
                Guid.NewGuid(),
                "integration-test@onecart.local",
                CancellationToken.None);
            Assert.Null(save.Errors);

            var published = await new StorefrontCatalogService(database).GetPublishedProductAsync(CancellationToken.None);
            Assert.NotNull(published);
            var variant = Assert.Single(published.Variants);
            Assert.Equal(19.95m, variant.Price);
            var group = Assert.Single(published.AttributeGroups);
            Assert.Equal("Memory", group.Name);
            var option = Assert.Single(group.Options);

            var quoteService = new CartQuoteService(
                database,
                new EphemeralDataProtectionProvider().CreateProtector("OneCart.CartQuote.v1"));
            var quote = await quoteService.CreateAsync(new CartQuoteRequest(variant.Id, [option.Id]), CancellationToken.None);
            Assert.NotNull(quote.Quote);

            var placed = await new CheckoutService(database, quoteService).PlaceOrderAsync(
                new OrderPlacementRequest(
                    "Integration Customer",
                    "+8801712345678",
                    "Integration test delivery address",
                    [new OrderLineRequest(quote.Quote!.Token, 2)]),
                CancellationToken.None);
            Assert.NotNull(placed.Order);
            Assert.Equal("Pending", placed.Order!.Status);
            Assert.Equal(5, await database.ProductVariants.AsNoTracking()
                .Where(item => item.Id == variant.Id)
                .Select(item => item.StockQuantity)
                .SingleAsync());

            var order = await database.StoreOrders.SingleAsync(item => item.OrderNumber == placed.Order.OrderNumber);
            var cancelled = await new OrderManagementService(database).UpdateStatusAsync(
                order.Id,
                "Cancelled",
                Guid.NewGuid(),
                "integration-test@onecart.local",
                CancellationToken.None);
            Assert.Equal("Cancelled", cancelled.Order?.Status);
            Assert.Equal(7, await database.ProductVariants.AsNoTracking()
                .Where(item => item.Id == variant.Id)
                .Select(item => item.StockQuantity)
                .SingleAsync());
        }
        finally
        {
            await database.Database.EnsureDeletedAsync();
            NpgsqlConnection.ClearAllPools();
        }
    }
}
