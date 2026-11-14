using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Data;

namespace OneCart.Api.Orders;

public sealed class CheckoutService(StoreDbContext database, CartQuoteService quotes)
{
    private const decimal MaximumStoredAmount = 99999999999999.9999m;

    public async Task<OrderPlacementResult> PlaceOrderAsync(OrderPlacementRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Any(line => line is null))
        {
            return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { [nameof(request.Lines)] = ["Add valid cart selections."] });
        }
        var errors = ValidateCustomer(request);
        if (errors.Count > 0) return OrderPlacementResult.Invalid(errors);

        var preparedLines = new List<PreparedOrderLine>(request.Lines.Count);
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            if (line.Quantity <= 0)
            {
                errors[$"Lines[{index}].Quantity"] = ["Quantity must be greater than zero."];
                continue;
            }
            if (string.IsNullOrWhiteSpace(line.QuoteToken) || line.QuoteToken.Length > 12000)
            {
                errors[$"Lines[{index}].QuoteToken"] = ["The cart price quote is invalid. Remove this item and add it again."];
                continue;
            }

            var quote = quotes.Read(line.QuoteToken);
            if (quote is null)
            {
                errors[$"Lines[{index}].QuoteToken"] = ["The cart price quote is invalid. Remove this item and add it again."];
                continue;
            }

            preparedLines.Add(new PreparedOrderLine(quote, line.Quantity));
        }
        if (errors.Count > 0) return OrderPlacementResult.Invalid(errors);
        if (preparedLines.Count == 0) return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { ["Lines"] = ["Your cart is empty."] });
        if (preparedLines.Select(line => line.Quote.CurrencyCode).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { ["Lines"] = ["An order can only contain one currency. Clear your cart and add items in a single currency."] });
        }

        var productId = preparedLines[0].Quote.ProductId;
        if (preparedLines.Any(line => line.Quote.ProductId != productId))
        {
            return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { ["Lines"] = ["The cart contains a quote for a different store product."] });
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var product = await database.Products
            .Include(candidate => candidate.Variants)
            .Include(candidate => candidate.AttributeGroups)
                .ThenInclude(group => group.Options)
            .SingleOrDefaultAsync(candidate => candidate.Id == productId && candidate.IsPublished, cancellationToken);
        if (product is null)
        {
            return OrderPlacementResult.Unavailable("The product is no longer available. Refresh the store and review your cart.");
        }

        foreach (var line in preparedLines)
        {
            var variant = product.Variants.SingleOrDefault(candidate => candidate.Id == line.Quote.VariantId && candidate.IsActive);
            if (variant is null)
            {
                return OrderPlacementResult.Unavailable("A product option in your cart is no longer available. Remove it and try again.");
            }

            var currentOptionIds = line.Quote.Options.Select(option => option.Id).ToHashSet();
            var currentOptions = product.AttributeGroups.SelectMany(group => group.Options)
                .Where(option => currentOptionIds.Contains(option.Id) && option.IsActive)
                .ToArray();
            if (currentOptions.Length != currentOptionIds.Count)
            {
                return OrderPlacementResult.Unavailable("A selected product option changed after it was added. Remove it and add the item again.");
            }
            foreach (var group in product.AttributeGroups)
            {
                var count = group.Options.Count(option => currentOptionIds.Contains(option.Id) && option.IsActive);
                if ((group.IsRequired && count == 0) || (!group.AllowMultiple && count > 1))
                {
                    return OrderPlacementResult.Unavailable("Product options changed after the item was added. Remove it and add the item again.");
                }
            }
        }

        var quantitiesByVariant = preparedLines
            .GroupBy(line => line.Quote.VariantId)
            .Select(group => new { VariantId = group.Key, Quantity = group.Sum(line => (long)line.Quantity) })
            .ToArray();
        if (quantitiesByVariant.Any(group => group.Quantity > int.MaxValue))
        {
            return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { ["Lines"] = ["The requested quantity is too large."] });
        }

        foreach (var requested in quantitiesByVariant)
        {
            var affected = await database.ProductVariants
                .Where(variant => variant.Id == requested.VariantId
                    && variant.ProductId == productId
                    && variant.IsActive
                    && variant.StockQuantity >= requested.Quantity)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(variant => variant.StockQuantity, variant => variant.StockQuantity - (int)requested.Quantity), cancellationToken);
            if (affected != 1)
            {
                return OrderPlacementResult.OutOfStock("The requested quantity is no longer available. Update your cart and try again.");
            }
        }

        decimal total;
        try
        {
            total = preparedLines.Sum(line => checked(line.Quote.UnitPrice * line.Quantity));
        }
        catch (OverflowException)
        {
            return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { ["Lines"] = ["The order total is too large."] });
        }
        if (total > MaximumStoredAmount)
        {
            return OrderPlacementResult.Invalid(new Dictionary<string, string[]> { ["Lines"] = ["The order total exceeds the supported amount."] });
        }

        var now = DateTimeOffset.UtcNow;
        var order = new StoreOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = await GenerateOrderNumberAsync(now, cancellationToken),
            Status = StoreOrderStatus.Pending,
            CustomerName = request.CustomerName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            CurrencyCode = preparedLines[0].Quote.CurrencyCode,
            Total = total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var prepared in preparedLines)
        {
            order.Lines.Add(new StoreOrderLine
            {
                Id = Guid.NewGuid(),
                VariantId = prepared.Quote.VariantId,
                ProductName = prepared.Quote.ProductName,
                Sku = prepared.Quote.Sku,
                VariantName = prepared.Quote.VariantName,
                SelectedOptionsJson = JsonSerializer.Serialize(prepared.Quote.Options),
                Quantity = prepared.Quantity,
                UnitPrice = prepared.Quote.UnitPrice
            });
        }

        database.StoreOrders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OrderPlacementResult.Placed(ToConfirmation(order));
    }

    public static Dictionary<string, string[]> ValidateCustomer(OrderPlacementRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Trim().Length > 200)
        {
            errors[nameof(request.CustomerName)] = ["Enter a customer name of 1 to 200 characters."];
        }
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Trim().Length > 50)
        {
            errors[nameof(request.PhoneNumber)] = ["Enter a phone number of 1 to 50 characters."];
        }
        if (string.IsNullOrWhiteSpace(request.DeliveryAddress) || request.DeliveryAddress.Trim().Length > 1000)
        {
            errors[nameof(request.DeliveryAddress)] = ["Enter a delivery address of 1 to 1,000 characters."];
        }
        if (request.Lines is null || request.Lines.Count is 0 or > 100)
        {
            errors[nameof(request.Lines)] = ["Add between 1 and 100 cart selections."];
        }
        return errors;
    }

    private async Task<string> GenerateOrderNumberAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(5));
            var orderNumber = $"OC-{now:yyyyMMdd}-{suffix}";
            if (!await database.StoreOrders.AnyAsync(order => order.OrderNumber == orderNumber, cancellationToken)) return orderNumber;
        }
        throw new InvalidOperationException("Could not allocate a unique order number.");
    }

    private static OrderConfirmationResponse ToConfirmation(StoreOrder order) => new(
        order.OrderNumber,
        order.Status,
        order.CurrencyCode,
        order.Total,
        order.CreatedAtUtc,
        order.Lines.Select(line => new OrderConfirmationLine(line.ProductName, line.VariantName, line.Quantity, line.UnitPrice)).ToArray());

    private sealed record PreparedOrderLine(CartQuotePayload Quote, int Quantity);
}

public sealed record OrderPlacementRequest(string CustomerName, string PhoneNumber, string DeliveryAddress, IReadOnlyList<OrderLineRequest> Lines);
public sealed record OrderLineRequest(string QuoteToken, int Quantity);
public sealed record OrderConfirmationResponse(string OrderNumber, string Status, string CurrencyCode, decimal Total, DateTimeOffset CreatedAtUtc, IReadOnlyList<OrderConfirmationLine> Lines);
public sealed record OrderConfirmationLine(string ProductName, string VariantName, int Quantity, decimal UnitPrice);

public sealed record OrderPlacementResult(OrderConfirmationResponse? Order, string? Error, Dictionary<string, string[]>? Errors, int StatusCode)
{
    public static OrderPlacementResult Placed(OrderConfirmationResponse order) => new(order, null, null, StatusCodes.Status201Created);
    public static OrderPlacementResult Invalid(Dictionary<string, string[]> errors) => new(null, null, errors, StatusCodes.Status400BadRequest);
    public static OrderPlacementResult Unavailable(string error) => new(null, error, null, StatusCodes.Status409Conflict);
    public static OrderPlacementResult OutOfStock(string error) => new(null, error, null, StatusCodes.Status409Conflict);
}
