namespace OneCart.Api.Catalog;

public sealed record StorefrontProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    IReadOnlyList<StorefrontVariantResponse> Variants);

public sealed record StorefrontVariantResponse(
    Guid Id,
    string DisplayName,
    decimal Price,
    string CurrencyCode,
    bool IsAvailable);
