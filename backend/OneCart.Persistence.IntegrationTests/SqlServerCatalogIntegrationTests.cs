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
                    true),
                 new AdminProductVariantRequest(
                    null,
                    $"IT-{Guid.NewGuid():N}",
                    "Deluxe",
                    29.95m,
                    "USD",
                    4,
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

            // Exercise the admin's normal edit path: update existing variant prices and stock,
            // add a new variant in the same save, then read all variants back from SQL Server.
            var savedProduct = saved.Product;
            var existingVariants = savedProduct.Variants.DistinctBy(variant => variant.Id).ToArray();
            var persistedVariantIds = await database.ProductVariants.AsNoTracking()
                .Where(variant => variant.ProductId == savedProduct.Id)
                .Select(variant => variant.Id)
                .ToListAsync();
            Assert.Equal(existingVariants.Select(variant => variant.Id).Distinct().Order(), persistedVariantIds.Order());
            var existingAttributeGroup = Assert.Single(savedProduct.AttributeGroups.DistinctBy(group => group.Id));
            Assert.True(await database.ProductAttributeGroups.AsNoTracking()
                .AnyAsync(group => group.Id == existingAttributeGroup.Id));
            var variantUpdate = request with
            {
                Variants =
                [
                    new AdminProductVariantRequest(existingVariants[0].Id, existingVariants[0].Sku,
                        "Standard", 21.50m, "USD", 11, true),
                    new AdminProductVariantRequest(existingVariants[1].Id, existingVariants[1].Sku,
                        "Deluxe", 31.50m, "USD", 6, true),
                    new AdminProductVariantRequest(null, $"IT-{Guid.NewGuid():N}",
                        "Premium", 39.95m, "USD", 2, true)
                ],
                AttributeGroups =
                [
                    new AdminProductAttributeGroupRequest(
                        existingAttributeGroup.Id,
                        existingAttributeGroup.Name,
                        existingAttributeGroup.IsRequired,
                        existingAttributeGroup.AllowMultiple,
                        existingAttributeGroup.DisplayOrder,
                        existingAttributeGroup.Options
                            .Select(option => new AdminProductAttributeOptionRequest(
                                option.Id, option.Name, option.PriceAdjustment, option.IsActive, option.DisplayOrder))
                            .Append(new AdminProductAttributeOptionRequest(null, "32 GB", 45m, true, 1)).ToArray()),
                    new AdminProductAttributeGroupRequest(
                        null,
                        "Finish",
                        false,
                        false,
                        1,
                        [new AdminProductAttributeOptionRequest(null, "Rose", 5m, true, 0)])
                ],
                TrustItems = savedProduct.TrustItems.DistinctBy(item => item.Id)
                    .Select(item => new AdminProductTrustItemRequest(
                        item.Id, item.Title, item.Description, item.IsActive, item.DisplayOrder))
                    .Append(new AdminProductTrustItemRequest(null, "Easy returns", "Return within 14 days", true, 1)).ToArray()
            };
            // Each HTTP request gets its own scoped DbContext in the API.
            await using var editDatabase = new StoreDbContext(options.Options);
            var updated = await new AdminCatalogService(editDatabase).SaveProductAsync(
                variantUpdate,
                Guid.NewGuid(),
                "integration-test@onecart.local",
                CancellationToken.None);
            Assert.Null(updated.Errors);
            Assert.Equal(3, updated.Product?.Variants.Count);
            Assert.Equal(21.50m, updated.Product!.Variants.Single(variant => variant.DisplayName == "Standard").Price);
            Assert.Equal(11, updated.Product.Variants.Single(variant => variant.DisplayName == "Standard").StockQuantity);
            Assert.Equal(31.50m, updated.Product.Variants.Single(variant => variant.DisplayName == "Deluxe").Price);
            Assert.Equal(6, updated.Product.Variants.Single(variant => variant.DisplayName == "Deluxe").StockQuantity);
            Assert.Equal(2, updated.Product.AttributeGroups.Count);
            Assert.Equal(2, updated.Product.AttributeGroups.Single(group => group.Name == "Memory").Options.Count);
            Assert.Contains(updated.Product.AttributeGroups, group => group.Name == "Finish");
            Assert.Equal(2, updated.Product.TrustItems.Count);

            var readService = new StorefrontCatalogService(database);
            var published = await readService.GetPublishedProductAsync(CancellationToken.None);

            Assert.NotNull(published);
            Assert.Equal("sql-server-integration-product", published.Slug);
            Assert.Equal(3, published.Variants.Count);
            Assert.Equal(21.50m, published.Variants.Single(variant => variant.DisplayName == "Standard").Price);
            Assert.Equal(11, await database.ProductVariants.AsNoTracking()
                .Where(variant => variant.DisplayName == "Standard")
                .Select(variant => variant.StockQuantity)
                .SingleAsync());
            Assert.True(published.Variants.Single(variant => variant.DisplayName == "Standard").IsAvailable);
            Assert.Equal(2, published.AttributeGroups.Count);
            Assert.Equal(25m, published.AttributeGroups.Single(group => group.Name == "Memory").Options.Single(option => option.Name == "16 GB").PriceAdjustment);
            Assert.Equal(45m, published.AttributeGroups.Single(group => group.Name == "Memory").Options.Single(option => option.Name == "32 GB").PriceAdjustment);
            Assert.Contains(published.AttributeGroups, group => group.Name == "Finish");
            Assert.True(published.FlashSale.Enabled);
            Assert.Equal("Integration sale", published.FlashSale.Label);
            Assert.Equal(19.35m, published.Variants.Single(variant => variant.DisplayName == "Standard").SalePrice);
            Assert.True(published.SocialSharingEnabled);
            Assert.Contains("https://wa.me/8801712345678?text=", published.WhatsAppContactUrl);
            Assert.Equal("Integration Brand", published.BrandName);
            Assert.Equal("Made for daily use", published.StorefrontHeadline);
            Assert.Equal(2, published.TrustItems.Count);

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
            var productOption = published.AttributeGroups.Single(group => group.Name == "Memory")
                .Options.Single(option => option.Name == "16 GB");
            var finishOption = published.AttributeGroups.Single(group => group.Name == "Finish")
                .Options.Single(option => option.Name == "Rose");
            var quote = await quoteService.CreateAsync(
                new CartQuoteRequest(
                    published.Variants.Single(variant => variant.DisplayName == "Standard").Id,
                    [productOption.Id, finishOption.Id]),
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
            var standardVariantId = published.Variants.Single(item => item.DisplayName == "Standard").Id;
            await using (var inventoryDatabase = new StoreDbContext(options.Options))
            Assert.Equal(9, await inventoryDatabase.ProductVariants.AsNoTracking()
                .Where(variant => variant.Id == standardVariantId)
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
            await using (var inventoryDatabase = new StoreDbContext(options.Options))
            Assert.Equal(11, await inventoryDatabase.ProductVariants.AsNoTracking()
                .Where(variant => variant.Id == standardVariantId)
                .Select(variant => variant.StockQuantity)
                .SingleAsync());
            var blockedProductDelete = await new ProductMediaService(database, new LocalProductImageStorage(mediaRoot))
                .DeleteProductAsync(Guid.NewGuid(), "integration-test@onecart.local", CancellationToken.None);
            Assert.Equal(ProductDeleteResult.HasOrderHistory, blockedProductDelete);
            Assert.NotNull(await new AdminCatalogService(database).GetProductAsync(CancellationToken.None));
            Assert.Equal(6, await database.AdminAuditEvents.AsNoTracking().CountAsync());
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
