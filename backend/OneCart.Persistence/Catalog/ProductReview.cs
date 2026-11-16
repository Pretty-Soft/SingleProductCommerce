namespace OneCart.Api.Catalog;

public sealed class ProductReview
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public StoreProduct Product { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed record StorefrontContentItem(Guid Id, string Title, string Description);
