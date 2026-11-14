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
