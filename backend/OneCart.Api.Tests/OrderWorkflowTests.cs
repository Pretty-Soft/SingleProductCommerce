using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class OrderWorkflowTests
{
    [Fact]
    public async Task CheckoutSnapshotsQuoteAndCancellationRestoresStockOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Computer", Slug = "computer", Description = "Configurable computer",
            CurrencyCode = "USD", IsPublished = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), Product = product, Sku = "PC-BASE", DisplayName = "Base", Price = 100,
            CurrencyCode = "USD", StockQuantity = 8, IsActive = true
        };
        var group = new ProductAttributeGroup
        {
            Id = Guid.NewGuid(), Product = product, Name = "Memory", IsRequired = true, AllowMultiple = false
        };
        var option = new ProductAttributeOption
        {
            Id = Guid.NewGuid(), AttributeGroup = group, Name = "16 GB", PriceAdjustment = 10, IsActive = true
        };
        product.Variants.Add(variant);
        product.AttributeGroups.Add(group);
        group.Options.Add(option);
        database.Products.Add(product);
        await database.SaveChangesAsync();

        var protector = new EphemeralDataProtectionProvider().CreateProtector("OneCart.CartQuote.v1");
        var quotes = new CartQuoteService(database, protector);
        var quoteResult = await quotes.CreateAsync(new CartQuoteRequest(variant.Id, [option.Id]), CancellationToken.None);
        Assert.NotNull(quoteResult.Quote);
        Assert.Equal(110m, quoteResult.Quote.UnitPrice);

        var checkout = new CheckoutService(database, quotes);
        var placed = await checkout.PlaceOrderAsync(new OrderPlacementRequest(
            "Ada Customer", "+1 555 0100", "123 Main Street", [new OrderLineRequest(quoteResult.Quote.Token, 2)]), CancellationToken.None);

        Assert.NotNull(placed.Order);
        Assert.StartsWith("OC-", placed.Order.OrderNumber);
        Assert.Equal("Pending", placed.Order.Status);
        Assert.Equal(220m, placed.Order.Total);
        Assert.Equal(6, await database.ProductVariants.Where(item => item.Id == variant.Id).Select(item => item.StockQuantity).SingleAsync());

        var storedOrder = await database.StoreOrders.Include(item => item.Lines).SingleAsync();
        Assert.Equal("USD", storedOrder.CurrencyCode);
        Assert.Equal(110m, storedOrder.Lines.Single().UnitPrice);
        Assert.Equal(2, storedOrder.Lines.Single().Quantity);

        var management = new OrderManagementService(database);
        var actorId = Guid.NewGuid();
        var cancelled = await management.UpdateStatusAsync(storedOrder.Id, "Cancelled", actorId, "admin@example.com", CancellationToken.None);
        Assert.NotNull(cancelled.Order);
        Assert.Equal("Cancelled", cancelled.Order.Status);
        Assert.Equal(8, await database.ProductVariants.Where(item => item.Id == variant.Id).Select(item => item.StockQuantity).SingleAsync());

        await management.UpdateStatusAsync(storedOrder.Id, "Cancelled", actorId, "admin@example.com", CancellationToken.None);
        Assert.Equal(8, await database.ProductVariants.Where(item => item.Id == variant.Id).Select(item => item.StockQuantity).SingleAsync());
        Assert.Single(await database.AdminAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task CheckoutRejectsTamperedQuoteWithoutChangingStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Computer", Slug = "computer", Description = "Configurable computer",
            CurrencyCode = "USD", IsPublished = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), Product = product, Sku = "PC-BASE", DisplayName = "Base", Price = 100,
            CurrencyCode = "USD", StockQuantity = 3, IsActive = true
        };
        product.Variants.Add(variant);
        database.Products.Add(product);
        await database.SaveChangesAsync();
        var quotes = new CartQuoteService(database, new EphemeralDataProtectionProvider().CreateProtector("OneCart.CartQuote.v1"));

        var result = await new CheckoutService(database, quotes).PlaceOrderAsync(new OrderPlacementRequest(
            "Ada Customer", "+1 555 0100", "123 Main Street", [new OrderLineRequest("not-a-signed-quote", 1)]), CancellationToken.None);

        Assert.Null(result.Order);
        Assert.NotNull(result.Errors);
        Assert.Equal(3, await database.ProductVariants.Where(item => item.Id == variant.Id).Select(item => item.StockQuantity).SingleAsync());
        Assert.Empty(await database.StoreOrders.ToListAsync());
    }
}
