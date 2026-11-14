using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;

namespace OneCart.Api.Catalog;

public sealed class StorefrontCatalogService(StoreDbContext database)
{
    public async Task<StorefrontProductResponse?> GetPublishedProductAsync(
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Where(candidate => candidate.IsPublished && candidate.Variants.Any(variant => variant.IsActive))
            .Include(candidate => candidate.Variants
                .Where(variant => variant.IsActive)
                .OrderBy(variant => variant.DisplayName))
            .Include(candidate => candidate.AttributeGroups
                .OrderBy(group => group.DisplayOrder))
                .ThenInclude(group => group.Options
                    .Where(option => option.IsActive)
                    .OrderBy(option => option.DisplayOrder))
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var variants = product.Variants
            .Select(variant => new StorefrontVariantResponse(
                variant.Id,
                variant.DisplayName,
                variant.Price,
                variant.CurrencyCode,
                variant.StockQuantity > 0))
            .ToArray();

        return new StorefrontProductResponse(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            variants,
            product.AttributeGroups
                .Select(group => new StorefrontAttributeGroupResponse(
                    group.Id,
                    group.Name,
                    group.IsRequired,
                    group.AllowMultiple,
                    group.Options.Select(option => new StorefrontAttributeOptionResponse(
                        option.Id,
                        option.Name,
                        option.PriceAdjustment)).ToArray()))
                .ToArray());
    }
}
