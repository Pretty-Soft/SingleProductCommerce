namespace OneCart.Api.Catalog;

public sealed class ProductAttributeGroup
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public StoreProduct Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public bool AllowMultiple { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<ProductAttributeOption> Options { get; } = new List<ProductAttributeOption>();
}
