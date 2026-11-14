using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;

namespace OneCart.Api.Catalog;

public sealed class StorefrontCatalogService(StoreDbContext database)
{
    public async Task<StorefrontProductResponse?> GetPublishedProductAsync(
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
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var serverTimeUtc = DateTimeOffset.UtcNow;
        var hasValidSaleWindow = product.FlashSaleStartsAtUtc is not null
            && product.FlashSaleEndsAtUtc is not null
            && product.FlashSaleEndsAtUtc > product.FlashSaleStartsAtUtc;
        var saleConfigured = product.FlashSaleEnabled && hasValidSaleWindow;

        var variants = product.Variants
            .Select(variant => new StorefrontVariantResponse(
                variant.Id,
                variant.DisplayName,
                variant.Price,
                saleConfigured ? CalculateSalePrice(variant.Price, product.FlashSaleDiscountType, product.FlashSaleDiscountValue) : null,
                variant.CurrencyCode,
                variant.StockQuantity > 0))
            .ToArray();

        var phoneDigits = new string(product.WhatsAppPhoneNumber.Where(char.IsDigit).ToArray());
        var whatsAppUrl = product.WhatsAppEnabled && phoneDigits.Length is >= 7 and <= 15
            ? $"https://wa.me/{phoneDigits}?text={Uri.EscapeDataString(product.WhatsAppPrefillMessage)}"
            : null;

        return new StorefrontProductResponse(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            variants,
            product.AttributeGroups
                .Select(group => new StorefrontAttributeGroupResponse(
                    group.Id,
                    group.Name,
                    group.IsRequired,
                    group.AllowMultiple,
                    group.Options.Select(option => new StorefrontAttributeOptionResponse(
                        option.Id,
                        option.Name,
                        option.PriceAdjustment)).ToArray()))
                .ToArray(),
            new StorefrontFlashSaleResponse(
                saleConfigured,
                product.FlashSaleLabel,
                product.FlashSaleDiscountType,
                product.FlashSaleDiscountValue,
                product.FlashSaleStartsAtUtc,
                product.FlashSaleEndsAtUtc,
                serverTimeUtc),
            product.SocialSharingEnabled,
            whatsAppUrl);
    }

    private static decimal CalculateSalePrice(decimal price, string discountType, decimal discountValue)
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
