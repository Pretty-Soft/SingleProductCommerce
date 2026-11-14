namespace OneCart.Api.Catalog;

public sealed class ProductImage
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public StoreProduct Product { get; set; } = null!;

    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string MediaKind { get; set; } = "product";

    public string AltText { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
