namespace OneCart.Api.Catalog;

public sealed class StoreProduct
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ProductVariant> Variants { get; } = new List<ProductVariant>();

    public ICollection<ProductAttributeGroup> AttributeGroups { get; } = new List<ProductAttributeGroup>();
}
