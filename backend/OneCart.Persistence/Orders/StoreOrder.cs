namespace OneCart.Api.Orders;

public sealed class StoreOrder
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string Status { get; set; } = StoreOrderStatus.Pending;

    public string CustomerName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<StoreOrderLine> Lines { get; } = new List<StoreOrderLine>();
}

public sealed class StoreOrderLine
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public StoreOrder Order { get; set; } = null!;

    public Guid VariantId { get; set; }

    public OneCart.Api.Catalog.ProductVariant Variant { get; set; } = null!;

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string SelectedOptionsJson { get; set; } = "[]";

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

public static class StoreOrderStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Pending, Processing, Completed, Cancelled];
}
