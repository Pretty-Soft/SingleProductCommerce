namespace OneCart.Api.Catalog;

public sealed class ProductAttributeOption
{
    public Guid Id { get; set; }

    public Guid AttributeGroupId { get; set; }

    public ProductAttributeGroup AttributeGroup { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal PriceAdjustment { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
