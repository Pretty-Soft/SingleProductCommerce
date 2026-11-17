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
    public async Task CustomerContactDirectoryKeepsEachOrderSeparateAndPaginates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        database.StoreOrders.AddRange(
            CreateOrder("OC-20261115-0001", "USD", "Pending", 100m, now),
            CreateOrder("OC-20261115-0002", "USD", "Completed", 200m, now.AddMinutes(1)),
            CreateOrder("OC-20261115-0003", "USD", "Cancelled", 50m, now.AddMinutes(2)));
        await database.SaveChangesAsync();

        var service = new OrderManagementService(database);
        var firstPage = await service.SearchCustomerContactsAsync(null, 1, 1, CancellationToken.None);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal("OC-20261115-0003", Assert.Single(firstPage.Items).OrderNumber);
        Assert.Equal(1, firstPage.Page);
        var lastPage = await service.SearchCustomerContactsAsync(null, 99, 1, CancellationToken.None);
        Assert.Equal(3, lastPage.Page);
        Assert.Equal("OC-20261115-0001", lastPage.Items.Single().OrderNumber);

        var matchingContact = await service.SearchCustomerContactsAsync("customer", 1, 25, CancellationToken.None);
        Assert.Equal(3, matchingContact.TotalCount);
        Assert.Equal(3, matchingContact.Items.Count);

        var matchingOrder = await service.SearchCustomerContactsAsync("0002", 1, 25, CancellationToken.None);
        var contact = Assert.Single(matchingOrder.Items);
        Assert.Equal("OC-20261115-0002", contact.OrderNumber);
        Assert.Equal("Customer", contact.CustomerName);
    }

    [Fact]
    public async Task OrderTotalsStayGroupedByHistoricalCurrencyAndStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        database.StoreOrders.AddRange(
            CreateOrder("OC-USD-1", "USD", "Pending", 100m, now),
            CreateOrder("OC-USD-2", "USD", "Pending", 25m, now.AddMinutes(1)),
            CreateOrder("OC-USD-3", "USD", "Cancelled", 40m, now.AddMinutes(2)),
            CreateOrder("OC-BDT-1", "BDT", "Completed", 3000m, now.AddMinutes(3)));
        await database.SaveChangesAsync();

        var summary = await new OrderManagementService(database).GetOrderTotalsAsync(CancellationToken.None);

        Assert.Equal(2, summary.Count);
        var usd = Assert.Single(summary, currency => currency.CurrencyCode == "USD");
        Assert.Collection(usd.Statuses,
            pending =>
            {
                Assert.Equal("Pending", pending.Status);
                Assert.Equal(2, pending.OrderCount);
                Assert.Equal(125m, pending.OrderTotal);
            },
            cancelled =>
            {
                Assert.Equal("Cancelled", cancelled.Status);
                Assert.Equal(1, cancelled.OrderCount);
                Assert.Equal(40m, cancelled.OrderTotal);
            });

        var bdt = Assert.Single(summary, currency => currency.CurrencyCode == "BDT");
        var completed = Assert.Single(bdt.Statuses);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(3000m, completed.OrderTotal);
    }

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
    public async Task QuoteRounds_the_final_variant_and_option_price_to_the_active_currency_precision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var product = new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Precision product", Slug = "precision-product", Description = "Rounding check",
            CurrencyCode = "USD", IsPublished = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), Product = product, Sku = "PRECISION-USD", DisplayName = "Standard", Price = 100.005m,
            CurrencyCode = "USD", StockQuantity = 1, IsActive = true
        };
        var group = new ProductAttributeGroup
        {
            Id = Guid.NewGuid(), Product = product, Name = "Finish", IsRequired = false, AllowMultiple = false
        };
        var option = new ProductAttributeOption
        {
            Id = Guid.NewGuid(), AttributeGroup = group, Name = "Polished", PriceAdjustment = 0.004m, IsActive = true
        };
        product.Variants.Add(variant);
        product.AttributeGroups.Add(group);
        group.Options.Add(option);
        database.Products.Add(product);
        await database.SaveChangesAsync();

        var quotes = new CartQuoteService(database, new EphemeralDataProtectionProvider().CreateProtector("OneCart.CartQuote.v1"));
        var result = await quotes.CreateAsync(new CartQuoteRequest(variant.Id, [option.Id]), CancellationToken.None);

        Assert.NotNull(result.Quote);
        Assert.Equal(100.01m, result.Quote.UnitPrice);
        Assert.Equal("USD", result.Quote.CurrencyCode);
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

    private static StoreOrder CreateOrder(string orderNumber, string currencyCode, string status, decimal total, DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        OrderNumber = orderNumber,
        CurrencyCode = currencyCode,
        Status = status,
        CustomerName = "Customer",
        PhoneNumber = "+15550100",
        DeliveryAddress = "1 Test Street",
        Total = total,
        CreatedAtUtc = createdAtUtc,
        UpdatedAtUtc = createdAtUtc
    };
}
