namespace OneCart.Api.Catalog;

public sealed record StorefrontProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string CurrencyCode,
    string BrandName,
    string StorefrontHeadline,
    string StorefrontSubtitle,
    IReadOnlyList<StorefrontVariantResponse> Variants,
    IReadOnlyList<StorefrontAttributeGroupResponse> AttributeGroups,
    StorefrontFlashSaleResponse FlashSale,
    bool SocialSharingEnabled,
    string? WhatsAppContactUrl,
    IReadOnlyList<ProductImageResponse> Images,
    string? BrandLogoUrl,
    IReadOnlyList<StorefrontTrustItemResponse> TrustItems,
    StorefrontThemeResponse Theme);

public sealed record StorefrontThemeResponse(
    string PrimaryColor,
    string AccentColor,
    string TextColor,
    string MutedTextColor,
    string BackgroundColor,
    string SurfaceColor,
    string BorderColor,
    int ProductTitleFontSizePx,
    int DetailsFontSizePx);

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

public sealed record StorefrontTrustItemResponse(string Title, string Description);
