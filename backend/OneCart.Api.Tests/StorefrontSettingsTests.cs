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
            true, true, "+15551234567", "Ask us about this product", [],
            Theme: new StorefrontThemeRequest("#171717", "#d17e8e", "#282326", "#777078", "#fbf8f8", "#ffffff", "#eee5e8", 16, 12),
            AboutUs: "Thoughtfully selected products for everyday care.");
        var result = await new StorefrontSettingsService(database).SaveAsync(
            request, Guid.NewGuid(), "owner@example.test", CancellationToken.None);

        Assert.False(result.IsNotFound);
        Assert.Null(result.Errors);
        Assert.Equal("Pretty Shop", result.Settings!.BrandName);
        Assert.Equal("Thoughtfully selected products for everyday care.", result.Settings.AboutUs);
        Assert.Equal(result.Settings.AboutUs, product.AboutUs);
        Assert.Equal("computer", product.Slug);
        Assert.Equal("Product description", product.Description);
        Assert.Equal("PC-1", variant.Sku);
        Assert.Equal(8, variant.StockQuantity);
        Assert.Equal("#d17e8e", result.Settings.Theme.AccentColor);
        Assert.Equal(16, result.Settings.Theme.ProductTitleFontSizePx);
        Assert.Equal(12, result.Settings.Theme.DetailsFontSizePx);
        Assert.Contains(database.AdminAuditEvents, item => item.Action == "storefront.settings.update");
    }

    [Fact]
    public async Task StoreManagerSettingsRejectInvalidThemeColorsAndFontSizes()
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
            true, false, "", "", [],
            Theme: new StorefrontThemeRequest("red", "#c17b88", "#282326", "#777078", "#fbf8f8", "#ffffff", "#eee5e8", 18, 10));
        var result = await new StorefrontSettingsService(database).SaveAsync(
            request, Guid.NewGuid(), "owner@example.test", CancellationToken.None);

        Assert.NotNull(result.Errors);
        Assert.Contains("Theme.PrimaryColor", result.Errors.Keys);
        Assert.Contains("Theme.ProductTitleFontSizePx", result.Errors.Keys);
        Assert.Contains("Theme.DetailsFontSizePx", result.Errors.Keys);
        Assert.Equal("#242020", (await database.Products.SingleAsync()).ThemePrimaryColor);
        Assert.Empty(database.AdminAuditEvents);
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
