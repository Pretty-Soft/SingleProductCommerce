using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class ProductLifecycleTests
{
    [Fact]
    public async Task Delete_product_removes_children_and_local_media_when_no_orders_exist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Test product", Slug = "test-product", CurrencyCode = "USD",
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        product.Variants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(), Product = product, Sku = "TEST-1", DisplayName = "Standard",
            Price = 10m, CurrencyCode = "USD", StockQuantity = 4, IsActive = true
        });
        database.Products.Add(product);
        await database.SaveChangesAsync();

        var mediaRoot = Path.Combine(Path.GetTempPath(), "onecart-product-delete-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalProductImageStorage(mediaRoot);
            var media = new ProductMediaService(database, storage);
            byte[] pngSignature = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
            var upload = await media.UploadAsync(new MemoryStream(pngSignature), pngSignature.Length, "product", "Product image", Guid.NewGuid(), "owner@example.test", CancellationToken.None);
            var image = Assert.IsType<ProductImageResponse>(upload.Image);
            var storageKey = await database.ProductImages.Where(item => item.Id == image.Id).Select(item => item.StorageKey).SingleAsync();
            Assert.True(File.Exists(Path.Combine(mediaRoot, storageKey)));

            var result = await media.DeleteProductAsync(Guid.NewGuid(), "owner@example.test", CancellationToken.None);

            Assert.Equal(ProductDeleteResult.Deleted, result);
            Assert.Equal(0, await database.Products.CountAsync());
            Assert.Equal(0, await database.ProductVariants.CountAsync());
            Assert.Equal(0, await database.ProductImages.CountAsync());
            Assert.False(File.Exists(Path.Combine(mediaRoot, storageKey)));
            Assert.Contains(database.AdminAuditEvents, item => item.Action == "catalog.product.delete");
        }
        finally
        {
            if (Directory.Exists(mediaRoot)) Directory.Delete(mediaRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Delete_product_preserves_order_history_and_catalog_when_order_lines_exist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Ordered product", Slug = "ordered-product", CurrencyCode = "USD",
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), Product = product, Sku = "ORDERED-1", DisplayName = "Standard",
            Price = 10m, CurrencyCode = "USD", StockQuantity = 4, IsActive = true
        };
        product.Variants.Add(variant);
        var order = new StoreOrder
        {
            Id = Guid.NewGuid(), OrderNumber = "OC-TEST-1", Status = StoreOrderStatus.Pending,
            CustomerName = "Test customer", PhoneNumber = "+10000000000", DeliveryAddress = "Test address",
            CurrencyCode = "USD", Total = 10m, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        order.Lines.Add(new StoreOrderLine
        {
            Id = Guid.NewGuid(), Order = order, Variant = variant, ProductName = product.Name,
            Sku = variant.Sku, VariantName = variant.DisplayName, Quantity = 1, UnitPrice = 10m
        });
        database.Products.Add(product);
        database.StoreOrders.Add(order);
        await database.SaveChangesAsync();

        var result = await new ProductMediaService(database, new LocalProductImageStorage(Path.GetTempPath()))
            .DeleteProductAsync(Guid.NewGuid(), "owner@example.test", CancellationToken.None);

        Assert.Equal(ProductDeleteResult.HasOrderHistory, result);
        Assert.Equal(1, await database.Products.CountAsync());
        Assert.Equal(1, await database.StoreOrderLines.CountAsync());
        Assert.DoesNotContain(database.AdminAuditEvents, item => item.Action == "catalog.product.delete");
    }
}
