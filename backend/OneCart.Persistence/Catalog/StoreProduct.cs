namespace OneCart.Api.Catalog;

public sealed class StoreProduct
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

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
}
