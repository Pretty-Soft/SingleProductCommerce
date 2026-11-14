namespace OneCart.Api.Catalog;

public sealed class ProductVariant
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public StoreProduct Product { get; set; } = null!;

    public string Sku { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; }
}
