using OneCart.Api.Catalog;
using Xunit;

namespace OneCart.Persistence.Tests;

public sealed class CatalogValidationTests
{
    [Fact]
    public void Accepts_a_valid_published_product_with_one_active_variant()
    {
        var errors = AdminCatalogService.ValidateProduct(ValidPublishedProduct(), null);

        Assert.Empty(errors);
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
        [Variant()]);

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
