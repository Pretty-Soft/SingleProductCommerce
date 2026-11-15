using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;
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
        var mediaRoot = Path.Combine(Path.GetTempPath(), "onecart-media-test-" + Guid.NewGuid().ToString("N"));
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
            request = request with
            {
                CurrencyCode = "USD",
                FlashSale = new AdminFlashSaleRequest(
                    true,
                    "Integration sale",
                    "percentage",
                    10m,
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    DateTimeOffset.UtcNow.AddHours(1)),
                SocialSharingEnabled = true,
                WhatsAppEnabled = true,
                WhatsAppPhoneNumber = "+880 1712-345678",
                WhatsAppPrefillMessage = "I have a question",
                BrandName = "Integration Brand",
                StorefrontHeadline = "Made for daily use",
                StorefrontSubtitle = "Thoughtful essentials",
                TrustItems = [new AdminProductTrustItemRequest(null, "Fast delivery", "Ships within two days", true, 0)]
            };

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
            Assert.True(published.FlashSale.Enabled);
            Assert.Equal("Integration sale", published.FlashSale.Label);
            Assert.Equal(17.955m, published.Variants[0].SalePrice);
            Assert.True(published.SocialSharingEnabled);
            Assert.Contains("https://wa.me/8801712345678?text=", published.WhatsAppContactUrl);
            Assert.Equal("Integration Brand", published.BrandName);
            Assert.Equal("Made for daily use", published.StorefrontHeadline);
            Assert.Single(published.TrustItems);

            var mediaService = new ProductMediaService(database, new LocalProductImageStorage(mediaRoot));
            byte[] pngSignature = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
            var invalid = await mediaService.UploadAsync(new MemoryStream([1, 2, 3]), 3, "product", "Invalid image", Guid.NewGuid(), "media@onecart.local", CancellationToken.None);
            Assert.NotNull(invalid.Error);

            var uploaded = await mediaService.UploadAsync(new MemoryStream(pngSignature), pngSignature.Length, "product", "Product front", Guid.NewGuid(), "media@onecart.local", CancellationToken.None);
            Assert.Null(uploaded.Error);
            var image = Assert.IsType<ProductImageResponse>(uploaded.Image);
            Assert.Single((await readService.GetPublishedProductAsync(CancellationToken.None))!.Images);

            var publicImage = await mediaService.OpenPublishedImageAsync(image.Id, CancellationToken.None);
            Assert.NotNull(publicImage);
            await using (var content = publicImage!.Content)
            await using (var copy = new MemoryStream())
            {
                await content.CopyToAsync(copy);
                Assert.Equal(pngSignature, copy.ToArray());
            }

            var update = await mediaService.UpdateAsync(image.Id, "Updated product front", 3, Guid.NewGuid(), "media@onecart.local", CancellationToken.None);
            Assert.Equal("Updated product front", update.Image?.AltText);
            Assert.True(await mediaService.DeleteAsync(image.Id, Guid.NewGuid(), "media@onecart.local", CancellationToken.None));
            Assert.Empty((await readService.GetPublishedProductAsync(CancellationToken.None))!.Images);
            var protector = new EphemeralDataProtectionProvider().CreateProtector("OneCart.CartQuote.v1");
            var quoteService = new CartQuoteService(database, protector);
            var productOption = published.AttributeGroups[0].Options[0];
            var quote = await quoteService.CreateAsync(
                new CartQuoteRequest(published.Variants[0].Id, [productOption.Id]),
                CancellationToken.None);
            Assert.NotNull(quote.Quote);

            var checkout = new CheckoutService(database, quoteService);
            var placement = await checkout.PlaceOrderAsync(
                new OrderPlacementRequest(
                    "Integration Customer",
                    "+8801712345678",
                    "Integration test delivery address",
                    [new OrderLineRequest(quote.Quote!.Token, 2)]),
                CancellationToken.None);
            Assert.NotNull(placement.Order);
            Assert.Equal("Pending", placement.Order!.Status);
            Assert.Equal(5, await database.ProductVariants.AsNoTracking()
                .Where(variant => variant.Id == published.Variants[0].Id)
                .Select(variant => variant.StockQuantity)
                .SingleAsync());

            var order = await database.StoreOrders.SingleAsync(order => order.OrderNumber == placement.Order.OrderNumber);
            var orderManagement = new OrderManagementService(database);
            var cancellation = await orderManagement.UpdateStatusAsync(
                order.Id,
                "Cancelled",
                Guid.NewGuid(),
                "integration-test@onecart.local",
                CancellationToken.None);
            Assert.Equal("Cancelled", cancellation.Order?.Status);
            Assert.Equal(7, await database.ProductVariants.AsNoTracking()
                .Where(variant => variant.Id == published.Variants[0].Id)
                .Select(variant => variant.StockQuantity)
                .SingleAsync());
            Assert.Equal(5, await database.AdminAuditEvents.AsNoTracking().CountAsync());
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

            if (Directory.Exists(mediaRoot) && Path.GetFullPath(mediaRoot).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(mediaRoot, recursive: true);
            }
        }
    }
}
