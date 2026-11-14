using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

namespace OneCart.Api.Orders;

public sealed class CartQuoteService(StoreDbContext database, IDataProtector protector)
{
    public async Task<CartQuoteResult> CreateAsync(CartQuoteRequest request, CancellationToken cancellationToken)
    {
        if (request.AttributeOptionIds is null || request.AttributeOptionIds.Length > 100)
        {
            return CartQuoteResult.Invalid("Choose valid product options.");
        }

        var product = await database.Products
            .AsNoTracking()
            .Where(candidate => candidate.IsPublished)
            .Include(candidate => candidate.Variants)
            .Include(candidate => candidate.AttributeGroups)
                .ThenInclude(group => group.Options)
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return CartQuoteResult.Unavailable("The product is not available.");

        var variant = product.Variants.SingleOrDefault(candidate => candidate.Id == request.VariantId && candidate.IsActive);
        if (variant is null || variant.StockQuantity <= 0)
        {
            return CartQuoteResult.Unavailable("This product option is not currently available.");
        }
        if (!string.Equals(variant.CurrencyCode, product.CurrencyCode, StringComparison.Ordinal))
        {
            return CartQuoteResult.Unavailable("The product currency needs administrator review before items can be added.");
        }

        var selectedIds = request.AttributeOptionIds.ToHashSet();
        if (selectedIds.Count != request.AttributeOptionIds.Length)
        {
            return CartQuoteResult.Invalid("A product option can only be selected once.");
        }

        var selected = new List<ProductAttributeOption>();
        foreach (var group in product.AttributeGroups)
        {
            var groupSelections = group.Options.Where(option => selectedIds.Contains(option.Id)).ToArray();
            if (groupSelections.Length > 1 && !group.AllowMultiple)
            {
                return CartQuoteResult.Invalid($"Select only one option for {group.Name}.");
            }
            if (group.IsRequired && groupSelections.Length == 0)
            {
                return CartQuoteResult.Invalid($"Choose an option for {group.Name}.");
            }
            selected.AddRange(groupSelections.Where(option => option.IsActive));
        }

        if (selected.Count != selectedIds.Count)
        {
            return CartQuoteResult.Invalid("One or more selected options are no longer available.");
        }

        var now = DateTimeOffset.UtcNow;
        var saleActive = product.FlashSaleEnabled
            && product.FlashSaleStartsAtUtc.HasValue
            && product.FlashSaleEndsAtUtc.HasValue
            && product.FlashSaleStartsAtUtc <= now
            && product.FlashSaleEndsAtUtc > now;
        var basePrice = saleActive
            ? StorefrontCatalogService.CalculateSalePrice(variant.Price, product.FlashSaleDiscountType, product.FlashSaleDiscountValue)
            : variant.Price;
        var unitPrice = basePrice + selected.Sum(option => option.PriceAdjustment);
        var payload = new CartQuotePayload(
            product.Id,
            product.Name,
            variant.Id,
            variant.Sku,
            variant.DisplayName,
            product.CurrencyCode,
            unitPrice,
            now,
            selected.Select(option => new CartQuoteOption(option.Id, option.Name, option.PriceAdjustment)).ToArray());
        var token = protector.Protect(JsonSerializer.Serialize(payload));
        return CartQuoteResult.Quoted(new CartQuoteResponse(token, product.CurrencyCode, unitPrice, now));
    }

    public CartQuotePayload? Read(string token)
    {
        try
        {
            var json = protector.Unprotect(token);
            return JsonSerializer.Deserialize<CartQuotePayload>(json);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed record CartQuoteRequest(Guid VariantId, Guid[] AttributeOptionIds);

public sealed record CartQuoteResponse(string Token, string CurrencyCode, decimal UnitPrice, DateTimeOffset QuotedAtUtc);

public sealed record CartQuotePayload(
    Guid ProductId,
    string ProductName,
    Guid VariantId,
    string Sku,
    string VariantName,
    string CurrencyCode,
    decimal UnitPrice,
    DateTimeOffset QuotedAtUtc,
    IReadOnlyList<CartQuoteOption> Options);

public sealed record CartQuoteOption(Guid Id, string Name, decimal PriceAdjustment);

public sealed record CartQuoteResult(CartQuoteResponse? Quote, string? Error, int StatusCode)
{
    public static CartQuoteResult Quoted(CartQuoteResponse quote) => new(quote, null, StatusCodes.Status200OK);
    public static CartQuoteResult Invalid(string error) => new(null, error, StatusCodes.Status400BadRequest);
    public static CartQuoteResult Unavailable(string error) => new(null, error, StatusCodes.Status409Conflict);
}
