namespace OneCart.Api.Catalog;

public sealed class StoreProduct
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string CatalogTranslationsJson { get; set; } = "{}";

    public string StorefrontTranslationsJson { get; set; } = "{}";

    public string BrandName { get; set; } = "Pretty Shop";

    public string StorefrontHeadline { get; set; } = string.Empty;

    public string AboutUs { get; set; } = string.Empty;

    public string StorefrontBenefitsJson { get; set; } = "[]";

    public string StorefrontUsageStepsJson { get; set; } = "[]";

    public string StorefrontSubtitle { get; set; } = string.Empty;

    public string ThemePrimaryColor { get; set; } = "#242020";

    public string ThemeAccentColor { get; set; } = "#c17b88";

    public string ThemeTextColor { get; set; } = "#282326";

    public string ThemeMutedTextColor { get; set; } = "#777078";

    public string ThemeBackgroundColor { get; set; } = "#fbf8f8";

    public string ThemeSurfaceColor { get; set; } = "#ffffff";

    public string ThemeBorderColor { get; set; } = "#eee5e8";

    public int ThemeProductTitleFontSizePx { get; set; } = 30;

    public int ThemeDetailsFontSizePx { get; set; } = 13;

    public string CurrencyCode { get; set; } = "";

    public bool FlashSaleEnabled { get; set; }

    public string FlashSaleLabel { get; set; } = string.Empty;

    public string FlashSaleDiscountType { get; set; } = "percentage";

    public decimal FlashSaleDiscountValue { get; set; }

    public DateTimeOffset? FlashSaleStartsAtUtc { get; set; }

    public DateTimeOffset? FlashSaleEndsAtUtc { get; set; }

    public bool SocialSharingEnabled { get; set; }

    public bool WhatsAppEnabled { get; set; }

    public string WhatsAppPhoneNumber { get; set; } = string.Empty;

    public string WhatsAppPrefillMessage { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ProductVariant> Variants { get; } = new List<ProductVariant>();

    public ICollection<ProductAttributeGroup> AttributeGroups { get; } = new List<ProductAttributeGroup>();

    public ICollection<ProductImage> Images { get; } = new List<ProductImage>();

    public ICollection<ProductTrustItem> TrustItems { get; } = new List<ProductTrustItem>();

    public ICollection<ProductReview> Reviews { get; } = new List<ProductReview>();
}
