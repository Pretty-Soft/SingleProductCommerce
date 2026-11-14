namespace OneCart.Api.Catalog;

public sealed record ProductImageResponse(Guid Id, string Url, string MediaKind, string AltText, int DisplayOrder);
