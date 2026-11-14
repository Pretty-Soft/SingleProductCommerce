using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Data;

namespace OneCart.Api.Catalog;

public sealed class AdminCatalogService(StoreDbContext database)
{
    public async Task<AdminProductResponse?> GetProductAsync(CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Include(candidate => candidate.Variants.OrderBy(variant => variant.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

        return product is null ? null : ToResponse(product);
    }

    public async Task<AdminCatalogSaveResult> SaveProductAsync(
        AdminProductRequest request,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var product = await database.Products
            .Include(candidate => candidate.Variants)
            .SingleOrDefaultAsync(cancellationToken);
        var errors = ValidateProduct(request, product);
        if (errors.Count > 0)
        {
            return AdminCatalogSaveResult.Invalid(errors);
        }

        var before = product is null ? null : CreateAuditSnapshot(product);

        product ??= new StoreProduct
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        product.Name = request.Name.Trim();
        product.Slug = request.Slug.Trim();
        product.Description = request.Description.Trim();
        product.IsPublished = request.IsPublished;
        product.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var existingVariants = product.Variants.ToDictionary(variant => variant.Id);
        var retainedIds = new HashSet<Guid>();
        foreach (var requestedVariant in request.Variants)
        {
            ProductVariant variant;
            if (requestedVariant.Id is Guid variantId)
            {
                variant = existingVariants[variantId];
                retainedIds.Add(variantId);
            }
            else
            {
                variant = new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    Product = product
                };
                product.Variants.Add(variant);
            }

            variant.Sku = requestedVariant.Sku.Trim().ToUpperInvariant();
            variant.DisplayName = requestedVariant.DisplayName.Trim();
            variant.Price = requestedVariant.Price;
            variant.CurrencyCode = requestedVariant.CurrencyCode.Trim().ToUpperInvariant();
            variant.StockQuantity = requestedVariant.StockQuantity;
            variant.IsActive = requestedVariant.IsActive;
        }

        foreach (var variant in existingVariants.Values.Where(variant => !retainedIds.Contains(variant.Id)))
        {
            database.ProductVariants.Remove(variant);
            product.Variants.Remove(variant);
        }

        if (database.Entry(product).State == EntityState.Detached)
        {
            database.Products.Add(product);
        }

        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            Action = "catalog.product.save",
            TargetId = product.Id,
            Target = product.Slug,
            Details = JsonSerializer.Serialize(new { before, after = CreateAuditSnapshot(product) }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminCatalogSaveResult.Saved(ToResponse(product));
    }

    public static Dictionary<string, string[]> ValidateProduct(AdminProductRequest request, StoreProduct? product)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var fieldErrors))
            {
                fieldErrors = [];
                errors[field] = fieldErrors;
            }

            fieldErrors.Add(message);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var slug = request.Slug?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        var hasNullVariant = request.Variants?.Any(variant => variant is null) == true;
        var variants = request.Variants?.Where(variant => variant is not null).ToList() ?? [];

        if (name.Length is 0 or > 200)
        {
            AddError(nameof(request.Name), "Product name must be between 1 and 200 characters.");
        }

        if (slug.Length is 0 or > 180 || !System.Text.RegularExpressions.Regex.IsMatch(
                slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            AddError(nameof(request.Slug), "Use a lowercase URL slug with letters, numbers, and single hyphens.");
        }

        if (description.Length > 8000)
        {
            AddError(nameof(request.Description), "Description must be 8,000 characters or fewer.");
        }

        if (variants.Count is 0 or > 100)
        {
            AddError(nameof(request.Variants), "Add between 1 and 100 variants.");
        }

        if (hasNullVariant)
        {
            AddError(nameof(request.Variants), "Variants cannot contain empty entries.");
        }

        if (request.IsPublished && variants.All(variant => !variant.IsActive))
        {
            AddError(nameof(request.Variants), "A published product needs at least one active variant.");
        }

        var duplicateIds = variants
            .Where(variant => variant.Id.HasValue)
            .GroupBy(variant => variant.Id!.Value)
            .Any(group => group.Count() > 1);
        if (duplicateIds)
        {
            AddError(nameof(request.Variants), "A variant can only appear once in the product.");
        }

        var normalizedSkus = variants
            .Select(variant => variant.Sku?.Trim().ToUpperInvariant() ?? string.Empty)
            .ToArray();
        if (normalizedSkus.Distinct(StringComparer.Ordinal).Count() != normalizedSkus.Length)
        {
            AddError(nameof(request.Variants), "Each variant must have a unique SKU.");
        }

        for (var index = 0; index < variants.Count; index++)
        {
            var variant = variants[index];
            var field = $"Variants[{index}]";
            var sku = normalizedSkus[index];
            var displayName = variant.DisplayName?.Trim() ?? string.Empty;
            var currencyCode = variant.CurrencyCode?.Trim() ?? string.Empty;

            if (sku.Length is 0 or > 64)
            {
                AddError(field, "SKU is required and must be 64 characters or fewer.");
            }

            if (displayName.Length is 0 or > 200)
            {
                AddError(field, "Variant display name is required and must be 200 characters or fewer.");
            }

            var normalizedCurrencyCode = currencyCode.ToUpperInvariant();
            if (normalizedCurrencyCode.Length != 3 || normalizedCurrencyCode.Any(character => character < 'A' || character > 'Z'))
            {
                AddError(field, "Currency code must contain three letters.");
            }

            if (variant.Price < 0 || variant.Price > 99999999999999.9999m || decimal.Round(variant.Price, 4) != variant.Price)
            {
                AddError(field, "Price must be non-negative and support up to four decimal places.");
            }

            if (variant.StockQuantity < 0)
            {
                AddError(field, "Stock quantity cannot be negative.");
            }

            if (variant.Id is Guid variantId && (product is null || product.Variants.All(existing => existing.Id != variantId)))
            {
                AddError(field, "Variant does not belong to this product.");
            }
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static AdminProductResponse ToResponse(StoreProduct product) => new(
        product.Id,
        product.Name,
        product.Slug,
        product.Description,
        product.IsPublished,
        product.Variants
            .OrderBy(variant => variant.DisplayName)
            .Select(variant => new AdminProductVariantResponse(
                variant.Id,
                variant.Sku,
                variant.DisplayName,
                variant.Price,
                variant.CurrencyCode,
                variant.StockQuantity,
                variant.IsActive))
            .ToArray());

    private static object CreateAuditSnapshot(StoreProduct product) => new
    {
        product.Name,
        product.Slug,
        product.Description,
        product.IsPublished,
        Variants = product.Variants
            .OrderBy(variant => variant.Sku)
            .Select(variant => new
            {
                variant.Sku,
                variant.DisplayName,
                variant.Price,
                variant.CurrencyCode,
                variant.StockQuantity,
                variant.IsActive
            })
            .ToArray()
    };
}

public sealed record AdminProductRequest(
    string Name,
    string Slug,
    string Description,
    bool IsPublished,
    IReadOnlyList<AdminProductVariantRequest> Variants);

public sealed record AdminProductVariantRequest(
    Guid? Id,
    string Sku,
    string DisplayName,
    decimal Price,
    string CurrencyCode,
    int StockQuantity,
    bool IsActive);

public sealed record AdminProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    bool IsPublished,
    IReadOnlyList<AdminProductVariantResponse> Variants);

public sealed record AdminProductVariantResponse(
    Guid Id,
    string Sku,
    string DisplayName,
    decimal Price,
    string CurrencyCode,
    int StockQuantity,
    bool IsActive);

public sealed record AdminCatalogSaveResult(
    AdminProductResponse? Product,
    Dictionary<string, string[]>? Errors)
{
    public static AdminCatalogSaveResult Saved(AdminProductResponse product) => new(product, null);

    public static AdminCatalogSaveResult Invalid(Dictionary<string, string[]> errors) => new(null, errors);
}
