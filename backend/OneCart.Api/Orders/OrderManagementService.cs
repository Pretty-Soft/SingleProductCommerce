using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Data;

namespace OneCart.Api.Orders;

public sealed class OrderManagementService(StoreDbContext database)
{
    public async Task<AdminCustomerContactPage> SearchCustomerContactsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.StoreOrders.AsNoTracking();
        var searchTerm = search?.Trim();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            var loweredSearch = searchTerm.ToLowerInvariant();
            query = query.Where(order => order.CustomerName.ToLower().Contains(loweredSearch)
                || order.PhoneNumber.Contains(searchTerm)
                || order.OrderNumber.ToLower().Contains(loweredSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, pageCount);
        var items = await query
            .OrderByDescending(order => order.OrderNumber)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new AdminCustomerOrderContact(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.CustomerName,
                order.PhoneNumber,
                order.DeliveryAddress,
                order.CurrencyCode,
                order.Total,
                order.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminCustomerContactPage(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<AdminOrderCurrencyTotals>> GetOrderTotalsAsync(CancellationToken cancellationToken)
    {
        var statuses = await database.StoreOrders.AsNoTracking()
            .GroupBy(order => new { order.CurrencyCode, order.Status })
            .Select(group => new
            {
                group.Key.CurrencyCode,
                group.Key.Status,
                OrderCount = group.Count(),
                OrderTotal = group.Sum(order => order.Total)
            })
            .ToListAsync(cancellationToken);

        return statuses
            .GroupBy(summary => summary.CurrencyCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new AdminOrderCurrencyTotals(
                group.Key,
                group.OrderBy(summary => StatusOrder(summary.Status))
                    .Select(summary => new AdminOrderStatusTotals(summary.Status, summary.OrderCount, summary.OrderTotal))
                    .ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<AdminOrderSummary>> GetRecentOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await database.StoreOrders.AsNoTracking()
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(100)
            .Select(order => new AdminOrderSummary(
                order.Id, order.OrderNumber, order.Status, order.CustomerName, order.PhoneNumber,
                order.CurrencyCode, order.Total, order.CreatedAtUtc, order.Lines.Sum(line => line.Quantity)))
            .ToListAsync(cancellationToken);
        return orders;
    }

    public async Task<AdminOrderDetails?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await database.StoreOrders.AsNoTracking()
            .Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        return order is null ? null : ToDetails(order);
    }

    public async Task<OrderStatusUpdateResult> UpdateStatusAsync(
        Guid orderId,
        string status,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        if (!StoreOrderStatus.All.Contains(status, StringComparer.Ordinal))
        {
            return OrderStatusUpdateResult.Invalid("Choose a valid order status.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await database.StoreOrders.Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        if (order is null) return OrderStatusUpdateResult.NotFound();
        if (order.Status == status) return OrderStatusUpdateResult.Updated(ToDetails(order));
        if (!CanTransition(order.Status, status))
        {
            return OrderStatusUpdateResult.Invalid($"Order status cannot change from {order.Status} to {status}.");
        }

        var previousStatus = order.Status;
        if (status == StoreOrderStatus.Cancelled)
        {
            foreach (var group in order.Lines.GroupBy(line => line.VariantId))
            {
                var quantity = group.Sum(line => (long)line.Quantity);
                if (quantity > int.MaxValue)
                {
                    return OrderStatusUpdateResult.Invalid("The cancelled order quantity cannot be restored safely.");
                }
                var restored = await database.ProductVariants
                    .Where(variant => variant.Id == group.Key)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(variant => variant.StockQuantity, variant => variant.StockQuantity + (int)quantity), cancellationToken);
                if (restored != 1)
                {
                    return OrderStatusUpdateResult.Invalid("The original product option is unavailable; stock could not be restored.");
                }
            }
        }

        order.Status = status;
        order.UpdatedAtUtc = DateTimeOffset.UtcNow;
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            Action = "order.status.update",
            TargetId = order.Id,
            Target = order.OrderNumber,
            Details = JsonSerializer.Serialize(new { previousStatus, status, stockRestored = status == StoreOrderStatus.Cancelled }),
            CreatedAtUtc = order.UpdatedAtUtc
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OrderStatusUpdateResult.Updated(ToDetails(order));
    }

    public static bool CanTransition(string current, string next) => current switch
    {
        StoreOrderStatus.Pending => next is StoreOrderStatus.Processing or StoreOrderStatus.Cancelled,
        StoreOrderStatus.Processing => next is StoreOrderStatus.Completed or StoreOrderStatus.Cancelled,
        _ => false
    };

    private static int StatusOrder(string status) => status switch
    {
        StoreOrderStatus.Pending => 0,
        StoreOrderStatus.Processing => 1,
        StoreOrderStatus.Completed => 2,
        StoreOrderStatus.Cancelled => 3,
        _ => int.MaxValue
    };

    private static AdminOrderDetails ToDetails(StoreOrder order) => new(
        order.Id,
        order.OrderNumber,
        order.Status,
        order.CustomerName,
        order.PhoneNumber,
        order.DeliveryAddress,
        order.CurrencyCode,
        order.Total,
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.Lines.Select(line => new AdminOrderLine(
            line.ProductName,
            line.Sku,
            line.VariantName,
            JsonSerializer.Deserialize<CartQuoteOption[]>(line.SelectedOptionsJson) ?? [],
            line.Quantity,
            line.UnitPrice)).ToArray());
}

public sealed record AdminOrderSummary(Guid Id, string OrderNumber, string Status, string CustomerName, string PhoneNumber, string CurrencyCode, decimal Total, DateTimeOffset CreatedAtUtc, int ItemCount);
public sealed record AdminCustomerOrderContact(Guid Id, string OrderNumber, string Status, string CustomerName, string PhoneNumber, string DeliveryAddress, string CurrencyCode, decimal OrderTotal, DateTimeOffset CreatedAtUtc);
public sealed record AdminCustomerContactPage(IReadOnlyList<AdminCustomerOrderContact> Items, int TotalCount, int Page, int PageSize);
public sealed record AdminOrderStatusTotals(string Status, int OrderCount, decimal OrderTotal);
public sealed record AdminOrderCurrencyTotals(string CurrencyCode, IReadOnlyList<AdminOrderStatusTotals> Statuses);
public sealed record AdminOrderDetails(Guid Id, string OrderNumber, string Status, string CustomerName, string PhoneNumber, string DeliveryAddress, string CurrencyCode, decimal Total, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, IReadOnlyList<AdminOrderLine> Lines);
public sealed record AdminOrderLine(string ProductName, string Sku, string VariantName, IReadOnlyList<CartQuoteOption> SelectedOptions, int Quantity, decimal UnitPrice);

public sealed record OrderStatusUpdateResult(AdminOrderDetails? Order, string? Error, bool IsNotFound)
{
    public static OrderStatusUpdateResult Updated(AdminOrderDetails order) => new(order, null, false);
    public static OrderStatusUpdateResult Invalid(string error) => new(null, error, false);
    public static OrderStatusUpdateResult NotFound() => new(null, null, true);
}
