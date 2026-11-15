using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;

namespace OneCart.Api.Catalog;

public sealed class StorefrontCatalogService(StoreDbContext database)
{
    public Task<StorefrontProductResponse?> GetPublishedProductAsync(CancellationToken cancellationToken) =>
        GetPublishedProductAsync("en", cancellationToken);

    public async Task<StorefrontProductResponse?> GetPublishedProductAsync(
        string language,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Where(candidate => candidate.IsPublished && candidate.Variants.Any(variant => variant.IsActive))
            .Include(candidate => candidate.Variants
                .Where(variant => variant.IsActive)
                .OrderBy(variant => variant.DisplayName))
            .Include(candidate => candidate.AttributeGroups
                .OrderBy(group => group.DisplayOrder))
                .ThenInclude(group => group.Options
                    .Where(option => option.IsActive)
                    .OrderBy(option => option.DisplayOrder))
            .Include(candidate => candidate.Images.OrderBy(image => image.DisplayOrder))
            .Include(candidate => candidate.TrustItems
                .Where(item => item.IsActive)
                .OrderBy(item => item.DisplayOrder))
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var catalogTranslations = CatalogTranslations.Parse(product.CatalogTranslationsJson);
        var storefrontTranslations = CatalogTranslations.Parse(product.StorefrontTranslationsJson);
        string Catalog(string key, string value) => CatalogTranslations.Resolve(catalogTranslations, language, key, value);
        string Store(string key, string value) => CatalogTranslations.Resolve(storefrontTranslations, language, key, value);
        var serverTimeUtc = DateTimeOffset.UtcNow;
        var hasValidSaleWindow = product.FlashSaleStartsAtUtc is not null
            && product.FlashSaleEndsAtUtc is not null
            && product.FlashSaleEndsAtUtc > product.FlashSaleStartsAtUtc;
        var saleConfigured = product.FlashSaleEnabled && hasValidSaleWindow;

        var variants = product.Variants
            .Select(variant => new StorefrontVariantResponse(
                variant.Id,
                Catalog($"variant.{variant.Id}.name", variant.DisplayName),
                variant.Price,
                saleConfigured ? CalculateSalePrice(variant.Price, product.FlashSaleDiscountType, product.FlashSaleDiscountValue) : null,
                product.CurrencyCode,
                variant.StockQuantity > 0))
            .ToArray();

        var phoneDigits = new string(product.WhatsAppPhoneNumber.Where(char.IsDigit).ToArray());
        var whatsAppMessage = Store("contact.whatsAppPrefillMessage", product.WhatsAppPrefillMessage);
        var whatsAppUrl = product.WhatsAppEnabled && phoneDigits.Length is >= 7 and <= 15
            ? $"https://wa.me/{phoneDigits}?text={Uri.EscapeDataString(whatsAppMessage)}"
            : null;

        return new StorefrontProductResponse(
            product.Id,
            Catalog("product.name", product.Name),
            product.Slug,
            Catalog("product.description", product.Description),
            product.CurrencyCode,
            Store("store.brandName", product.BrandName),
            Store("store.headline", product.StorefrontHeadline),
            Store("store.subtitle", product.StorefrontSubtitle),
            variants,
            product.AttributeGroups
                .Select(group => new StorefrontAttributeGroupResponse(
                    group.Id,
                    Catalog($"attributeGroup.{group.Id}.name", group.Name),
                    group.IsRequired,
                    group.AllowMultiple,
                    group.Options.Select(option => new StorefrontAttributeOptionResponse(
                        option.Id,
                        Catalog($"attributeOption.{option.Id}.name", option.Name),
                        option.PriceAdjustment)).ToArray()))
                .ToArray(),
            new StorefrontFlashSaleResponse(
                saleConfigured,
                Store("promotion.flashSaleLabel", product.FlashSaleLabel),
                product.FlashSaleDiscountType,
                product.FlashSaleDiscountValue,
                product.FlashSaleStartsAtUtc,
                product.FlashSaleEndsAtUtc,
                serverTimeUtc),
            product.SocialSharingEnabled,
            whatsAppUrl,
            product.Images.Where(image => image.MediaKind == "product")
                .OrderBy(image => image.DisplayOrder)
                .Select(image => new ProductImageResponse(image.Id, "/api/v1/store/product/images/" + image.Id, image.MediaKind, image.AltText, image.DisplayOrder))
                .ToArray(),
            product.Images.Where(image => image.MediaKind == "logo")
                .Select(image => "/api/v1/store/product/images/" + image.Id)
                .SingleOrDefault(),
            product.TrustItems.OrderBy(item => item.DisplayOrder)
                .Select(item => new StorefrontTrustItemResponse(
                    Store($"trust.{item.Id}.title", item.Title),
                    Store($"trust.{item.Id}.description", item.Description)))
                .ToArray());
    }

    public static decimal CalculateSalePrice(decimal price, string discountType, decimal discountValue)
    {
        var discount = discountType switch
        {
            "percentage" => price * discountValue / 100m,
            "fixedAmount" => discountValue,
            _ => 0m
        };

        return decimal.Round(Math.Max(0m, price - discount), 4, MidpointRounding.AwayFromZero);
    }
}
