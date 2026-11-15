using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class StorefrontSettingsTests
{
    [Fact]
    public async Task SystemAdminCanPersistGlobalOptionalOrRequiredMfaPolicy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();
        var settings = new AdminSecuritySettingsService(database);

        Assert.False(await settings.GetMfaRequiredAsync(CancellationToken.None));
        Assert.True(await settings.SetMfaRequiredAsync(true, Guid.NewGuid(), "owner@example.test", CancellationToken.None));
        Assert.True(await settings.GetMfaRequiredAsync(CancellationToken.None));
        Assert.Contains(database.AdminAuditEvents, item => item.Action == "admin_security.mfa_policy.update");
    }

    [Fact]
    public async Task StoreManagerSettingsUpdateStorefrontFieldsWithoutChangingCatalog()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();
        var product = new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Computer", Slug = "computer", Description = "Product description",
            CurrencyCode = "USD", BrandName = "Old brand", CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), Product = product, Sku = "PC-1", DisplayName = "Base", Price = 100m,
            CurrencyCode = "USD", StockQuantity = 8, IsActive = true
        };
        product.Variants.Add(variant);
        database.Products.Add(product);
        await database.SaveChangesAsync();

        var request = new StorefrontSettingsRequest(
            "Pretty Shop", "Your trust, Your Love", "Subtitle",
            new AdminFlashSaleRequest(false, "", "percentage", 0, null, null),
            true, true, "+15551234567", "Ask us about this product", []);
        var result = await new StorefrontSettingsService(database).SaveAsync(
            request, Guid.NewGuid(), "owner@example.test", CancellationToken.None);

        Assert.False(result.IsNotFound);
        Assert.Null(result.Errors);
        Assert.Equal("Pretty Shop", result.Settings!.BrandName);
        Assert.Equal("computer", product.Slug);
        Assert.Equal("Product description", product.Description);
        Assert.Equal("PC-1", variant.Sku);
        Assert.Equal(8, variant.StockQuantity);
        Assert.Contains(database.AdminAuditEvents, item => item.Action == "storefront.settings.update");
    }

    [Fact]
    public async Task StoreManagerSettingsRejectInvalidWhatsAppContact()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
        await using var database = new StoreDbContext(options);
        await database.Database.EnsureCreatedAsync();
        database.Products.Add(new StoreProduct
        {
            Id = Guid.NewGuid(), Name = "Computer", Slug = "computer", CurrencyCode = "USD",
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync();

        var request = new StorefrontSettingsRequest(
            "Pretty Shop", "", "",
            new AdminFlashSaleRequest(false, "", "percentage", 0, null, null),
            true, true, "invalid number", "Hello", []);
        var result = await new StorefrontSettingsService(database).SaveAsync(
            request, Guid.NewGuid(), "owner@example.test", CancellationToken.None);

        Assert.NotNull(result.Errors);
        Assert.Contains(nameof(request.WhatsAppPhoneNumber), result.Errors.Keys);
        Assert.Equal("Pretty Shop", (await database.Products.SingleAsync()).BrandName);
        Assert.Empty(database.AdminAuditEvents);
    }
}
