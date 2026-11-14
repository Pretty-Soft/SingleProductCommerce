namespace OneCart.Api.Catalog;

public sealed class ProductTrustItem
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public StoreProduct Product { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
