using OneCart.Api.Catalog;
using Xunit;

namespace OneCart.Persistence.Tests;

public sealed class CatalogValidationTests
{
    [Fact]
    public void Resolves_requested_language_and_falls_back_to_english()
    {
        var translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["bn"] = new() { ["product.name"] = "বাংলা নাম" }
        };

        Assert.Equal("বাংলা নাম", CatalogTranslations.Resolve(translations, "bn", "product.name", "English name"));
        Assert.Equal("English name", CatalogTranslations.Resolve(translations, "es", "product.name", "English name"));
        Assert.Equal("English name", CatalogTranslations.Resolve(translations, "bn", "product.description", "English name"));
    }

    [Fact]
    public void Rejects_languages_outside_the_supported_three()
    {
        var errors = CatalogTranslations.Validate(new Dictionary<string, Dictionary<string, string>>
        {
            ["fr"] = new() { ["product.name"] = "nom" }
        }, "translations");

        Assert.Contains("translations.fr", errors.Keys);
    }

    [Fact]
    public void Accepts_a_valid_published_product_with_one_active_variant()
    {
        var errors = AdminCatalogService.ValidateProduct(ValidPublishedProduct(), null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Requires_explicit_repricing_before_currency_change()
    {
        var existingProduct = new StoreProduct { CurrencyCode = "USD" };
        var request = ValidPublishedProduct() with { CurrencyCode = "BDT" };

        var errors = AdminCatalogService.ValidateProduct(request, existingProduct);

        Assert.Contains(nameof(AdminProductRequest.CurrencyCode), errors.Keys);
    }

    [Fact]
    public void Allows_currency_change_after_product_deletion_when_no_order_is_open()
    {
        var request = ValidPublishedProduct() with
        {
            CurrencyCode = "EUR",
            Variants = [Variant() with { CurrencyCode = "EUR" }]
        };

        var errors = AdminCatalogService.ValidateProduct(request, null, "BDT");

        Assert.DoesNotContain(nameof(AdminProductRequest.CurrencyCode), errors.Keys);
    }

    [Fact]
    public void Blocks_currency_change_while_an_order_is_pending_or_processing()
    {
        var request = ValidPublishedProduct() with
        {
            CurrencyCode = "EUR", CurrencyRepricingConfirmed = true,
            Variants = [Variant() with { CurrencyCode = "EUR" }]
        };

        var errors = AdminCatalogService.ValidateProduct(request, new StoreProduct { CurrencyCode = "BDT" }, "BDT", hasOpenOrders: true);

        Assert.Contains(nameof(AdminProductRequest.CurrencyCode), errors.Keys);
    }

    [Fact]
    public void Allows_confirmed_repricing_when_all_orders_are_closed()
    {
        var request = ValidPublishedProduct() with
        {
            CurrencyCode = "EUR", CurrencyRepricingConfirmed = true,
            Variants = [Variant() with { CurrencyCode = "EUR" }]
        };

        var errors = AdminCatalogService.ValidateProduct(request, new StoreProduct { CurrencyCode = "BDT" }, "BDT", hasOpenOrders: false);

        Assert.DoesNotContain(nameof(AdminProductRequest.CurrencyCode), errors.Keys);
    }

    [Fact]
    public void Requires_new_variant_prices_and_disables_old_sale_on_currency_change()
    {
        var request = ValidPublishedProduct() with
        {
            CurrencyCode = "EUR", CurrencyRepricingConfirmed = true,
            Variants = [Variant() with { CurrencyCode = "EUR", Price = 0 }],
            FlashSale = new AdminFlashSaleRequest(true, "Old sale", "fixedAmount", 5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
        };

        var errors = AdminCatalogService.ValidateProduct(request, new StoreProduct { CurrencyCode = "BDT" }, "BDT");

        Assert.Contains(nameof(AdminProductRequest.Variants), errors.Keys);
        Assert.Contains(nameof(AdminProductRequest.FlashSale), errors.Keys);
    }

    [Fact]
    public void Requires_each_retained_price_to_be_reentered_before_currency_change()
    {
        var variantId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var product = new StoreProduct { CurrencyCode = "BDT" };
        product.Variants.Add(new ProductVariant { Id = variantId, Price = 100 });
        var group = new ProductAttributeGroup();
        group.Options.Add(new ProductAttributeOption { Id = optionId, PriceAdjustment = 10 });
        product.AttributeGroups.Add(group);
        var request = ValidPublishedProduct() with
        {
            CurrencyCode = "EUR",
            CurrencyRepricingConfirmed = true,
            Variants = [Variant(variantId) with { CurrencyCode = "EUR", Price = 5 }],
            AttributeGroups = [new AdminProductAttributeGroupRequest(null, "Size", true, false, 0,
                [new AdminProductAttributeOptionRequest(optionId, "Large", 2, true, 0)])]
        };

        var missing = AdminCatalogService.ValidateProduct(request, product, "BDT");
        Assert.Contains(nameof(AdminProductRequest.RepricedVariantIds), missing.Keys);
        Assert.Contains(nameof(AdminProductRequest.RepricedOptionIds), missing.Keys);

        var entered = AdminCatalogService.ValidateProduct(request with
        {
            RepricedVariantIds = [variantId],
            RepricedOptionIds = [optionId]
        }, product, "BDT");
        Assert.DoesNotContain(nameof(AdminProductRequest.RepricedVariantIds), entered.Keys);
        Assert.DoesNotContain(nameof(AdminProductRequest.RepricedOptionIds), entered.Keys);
    }

    [Fact]
    public void Requires_all_variants_to_use_the_store_currency()
    {
        var request = ValidPublishedProduct() with
        {
            Variants = [Variant() with { CurrencyCode = "BDT" }]
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Contains("Variants[0]", errors.Keys);
    }

    [Fact]
    public void Rejects_duplicate_skus_and_negative_stock()
    {
        var request = ValidPublishedProduct() with
        {
            Variants =
            [
                Variant(sku: "SKU-1"),
                Variant(sku: "sku-1", stock: -1)
            ]
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Contains(nameof(AdminProductRequest.Variants), errors.Keys);
        Assert.Contains("Variants[1]", errors.Keys);
    }

    [Fact]
    public void Rejects_a_variant_id_that_does_not_belong_to_the_product()
    {
        var request = ValidPublishedProduct() with
        {
            Variants = [Variant(id: Guid.NewGuid())]
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Contains("Variants[0]", errors.Keys);
    }

    [Fact]
    public void Accepts_required_single_choice_and_optional_multi_choice_attributes()
    {
        var request = ValidPublishedProduct() with
        {
            AttributeGroups =
            [
                new AdminProductAttributeGroupRequest(null, "Memory", true, false, 0,
                [new AdminProductAttributeOptionRequest(null, "16 GB", 0, true, 0)]),
                new AdminProductAttributeGroupRequest(null, "Software", false, true, 1,
                [new AdminProductAttributeOptionRequest(null, "Office", 49.99m, true, 0)])
            ]
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_required_attributes_without_active_choices_and_negative_adjustments()
    {
        var request = ValidPublishedProduct() with
        {
            AttributeGroups =
            [new AdminProductAttributeGroupRequest(null, "Processor", true, false, 0,
                [new AdminProductAttributeOptionRequest(null, "Unavailable", -1, false, 0)])]
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Contains("AttributeGroups[0]", errors.Keys);
        Assert.Contains("AttributeGroups[0].Options[0]", errors.Keys);
    }

    [Fact]
    public void Accepts_a_scheduled_flash_sale_and_valid_whats_app_number()
    {
        var request = ValidPublishedProduct() with
        {
            FlashSale = new AdminFlashSaleRequest(
                true,
                "Weekend sale",
                "percentage",
                15,
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow.AddHours(2)),
            WhatsAppEnabled = true,
            WhatsAppPhoneNumber = "+880 1712-345678",
            WhatsAppPrefillMessage = "Question about the product"
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_invalid_flash_sale_window_and_whats_app_number()
    {
        var now = DateTimeOffset.UtcNow;
        var request = ValidPublishedProduct() with
        {
            FlashSale = new AdminFlashSaleRequest(true, "Sale", "fixedAmount", 10m, now, now),
            WhatsAppEnabled = true,
            WhatsAppPhoneNumber = "not-a-phone"
        };

        var errors = AdminCatalogService.ValidateProduct(request, null);

        Assert.Contains(nameof(AdminProductRequest.FlashSale), errors.Keys);
        Assert.Contains(nameof(AdminProductRequest.WhatsAppPhoneNumber), errors.Keys);
    }

    private static AdminProductRequest ValidPublishedProduct() => new(
        "OneCart Sample",
        "onecart-sample",
        "A sample product description.",
        true,
        [Variant()],
        CurrencyCode: "USD");

    private static AdminProductVariantRequest Variant(
        Guid? id = null,
        string sku = "SKU-1",
        int stock = 5) => new(
            id,
            sku,
            "Standard",
            10.00m,
            "USD",
            stock,
            true);
}
