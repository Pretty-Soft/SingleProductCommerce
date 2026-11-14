namespace OneCart.Api.Catalog;

public sealed record StorefrontProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    IReadOnlyList<StorefrontVariantResponse> Variants,
    IReadOnlyList<StorefrontAttributeGroupResponse> AttributeGroups,
    StorefrontFlashSaleResponse FlashSale,
    bool SocialSharingEnabled,
    string? WhatsAppContactUrl);

public sealed record StorefrontVariantResponse(
    Guid Id,
    string DisplayName,
    decimal Price,
    decimal? SalePrice,
    string CurrencyCode,
    bool IsAvailable);

public sealed record StorefrontAttributeGroupResponse(
    Guid Id,
    string Name,
    bool IsRequired,
    bool AllowMultiple,
    IReadOnlyList<StorefrontAttributeOptionResponse> Options);

public sealed record StorefrontAttributeOptionResponse(
    Guid Id,
    string Name,
    decimal PriceAdjustment);

public sealed record StorefrontFlashSaleResponse(
    bool Enabled,
    string Label,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset ServerTimeUtc);
