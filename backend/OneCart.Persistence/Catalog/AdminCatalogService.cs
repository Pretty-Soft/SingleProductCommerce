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
            .Include(candidate => candidate.AttributeGroups.OrderBy(group => group.DisplayOrder))
                .ThenInclude(group => group.Options.OrderBy(option => option.DisplayOrder))
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
            .Include(candidate => candidate.AttributeGroups)
                .ThenInclude(group => group.Options)
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
        var flashSale = request.FlashSale ?? AdminFlashSaleRequest.Disabled;
        product.FlashSaleEnabled = flashSale.Enabled;
        product.FlashSaleLabel = flashSale.Label?.Trim() ?? string.Empty;
        product.FlashSaleDiscountType = flashSale.DiscountType?.Trim() ?? "percentage";
        product.FlashSaleDiscountValue = flashSale.DiscountValue;
        product.FlashSaleStartsAtUtc = flashSale.StartsAtUtc?.ToUniversalTime();
        product.FlashSaleEndsAtUtc = flashSale.EndsAtUtc?.ToUniversalTime();
        product.SocialSharingEnabled = request.SocialSharingEnabled;
        product.WhatsAppEnabled = request.WhatsAppEnabled;
        product.WhatsAppPhoneNumber = request.WhatsAppPhoneNumber?.Trim() ?? string.Empty;
        product.WhatsAppPrefillMessage = request.WhatsAppPrefillMessage?.Trim() ?? string.Empty;
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

        var existingGroups = product.AttributeGroups.ToDictionary(group => group.Id);
        var retainedGroupIds = new HashSet<Guid>();
        foreach (var requestedGroup in request.AttributeGroups ?? [])
        {
            ProductAttributeGroup group;
            if (requestedGroup.Id is Guid groupId)
            {
                group = existingGroups[groupId];
                retainedGroupIds.Add(groupId);
            }
            else
            {
                group = new ProductAttributeGroup { Id = Guid.NewGuid(), Product = product };
                product.AttributeGroups.Add(group);
            }

            group.Name = requestedGroup.Name.Trim();
            group.IsRequired = requestedGroup.IsRequired;
            group.AllowMultiple = requestedGroup.AllowMultiple;
            group.DisplayOrder = requestedGroup.DisplayOrder;

            var existingOptions = group.Options.ToDictionary(option => option.Id);
            var retainedOptionIds = new HashSet<Guid>();
            foreach (var requestedOption in requestedGroup.Options ?? [])
            {
                ProductAttributeOption option;
                if (requestedOption.Id is Guid optionId)
                {
                    option = existingOptions[optionId];
                    retainedOptionIds.Add(optionId);
                }
                else
                {
                    option = new ProductAttributeOption { Id = Guid.NewGuid(), AttributeGroup = group };
                    group.Options.Add(option);
                }

                option.Name = requestedOption.Name.Trim();
                option.PriceAdjustment = requestedOption.PriceAdjustment;
                option.IsActive = requestedOption.IsActive;
                option.DisplayOrder = requestedOption.DisplayOrder;
            }

            foreach (var option in existingOptions.Values.Where(option => !retainedOptionIds.Contains(option.Id)))
            {
                database.ProductAttributeOptions.Remove(option);
                group.Options.Remove(option);
            }
        }

        foreach (var group in existingGroups.Values.Where(group => !retainedGroupIds.Contains(group.Id)))
        {
            database.ProductAttributeGroups.Remove(group);
            product.AttributeGroups.Remove(group);
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

        var flashSale = request.FlashSale ?? AdminFlashSaleRequest.Disabled;
        if (flashSale.Enabled)
        {
            if (string.IsNullOrWhiteSpace(flashSale.Label) || flashSale.Label.Trim().Length > 100)
            {
                AddError(nameof(request.FlashSale), "A flash sale needs a label up to 100 characters.");
            }
            if (flashSale.DiscountType is not ("percentage" or "fixedAmount"))
            {
                AddError(nameof(request.FlashSale), "Choose a percentage or fixed amount discount.");
            }
            if (flashSale.DiscountType == "percentage"
                && (flashSale.DiscountValue <= 0 || flashSale.DiscountValue > 100 || decimal.Round(flashSale.DiscountValue, 2) != flashSale.DiscountValue))
            {
                AddError(nameof(request.FlashSale), "Percentage discount must be greater than 0 and no more than 100.");
            }
            if (flashSale.DiscountType == "fixedAmount"
                && (flashSale.DiscountValue <= 0 || flashSale.DiscountValue > 99999999999999.9999m || decimal.Round(flashSale.DiscountValue, 4) != flashSale.DiscountValue))
            {
                AddError(nameof(request.FlashSale), "Fixed discount must be greater than 0 and support up to four decimal places.");
            }
            if (flashSale.StartsAtUtc is null || flashSale.EndsAtUtc is null || flashSale.EndsAtUtc <= flashSale.StartsAtUtc)
            {
                AddError(nameof(request.FlashSale), "Set a valid sale start and end time; the end must be later than the start.");
            }
            if (flashSale.DiscountType == "fixedAmount"
                && variants.Where(variant => variant.IsActive).Any(variant => variant.Price <= flashSale.DiscountValue))
            {
                AddError(nameof(request.FlashSale), "The fixed discount must be less than every active variant price.");
            }
        }

        if (request.WhatsAppPhoneNumber is { Length: > 20 })
        {
            AddError(nameof(request.WhatsAppPhoneNumber), "WhatsApp phone number must be 20 characters or fewer.");
        }
        var waNumber = request.WhatsAppPhoneNumber?.Trim() ?? string.Empty;
        var waDigits = new string(waNumber.Where(char.IsDigit).ToArray());
        var allowedPhoneCharacters = System.Text.RegularExpressions.Regex.IsMatch(waNumber, @"^\+?[0-9\s()\-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (request.WhatsAppEnabled && (!allowedPhoneCharacters || waDigits.Length is < 7 or > 15))
        {
            AddError(nameof(request.WhatsAppPhoneNumber), "Enter a valid WhatsApp number with 7 to 15 digits, including country code.");
        }
        if ((request.WhatsAppPrefillMessage?.Length ?? 0) > 500)
        {
            AddError(nameof(request.WhatsAppPrefillMessage), "WhatsApp message must be 500 characters or fewer.");
        }

        var groups = request.AttributeGroups?.Where(group => group is not null).ToList() ?? [];
        if (request.AttributeGroups?.Any(group => group is null) == true)
        {
            AddError(nameof(request.AttributeGroups), "Attribute groups cannot contain empty entries.");
        }
        if (groups.Count > 30)
        {
            AddError(nameof(request.AttributeGroups), "A product can have up to 30 attribute groups.");
        }
        if (groups.Where(group => group.Id.HasValue).Select(group => group.Id).Distinct().Count() != groups.Count(group => group.Id.HasValue))
        {
            AddError(nameof(request.AttributeGroups), "An attribute group can only appear once in the product.");
        }
        var groupNames = groups.Select(group => group.Name?.Trim() ?? string.Empty).ToArray();
        if (groupNames.Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != groupNames.Count(value => value.Length > 0))
        {
            AddError(nameof(request.AttributeGroups), "Attribute group names must be unique.");
        }

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            var field = $"AttributeGroups[{groupIndex}]";
            if (string.IsNullOrWhiteSpace(group.Name) || group.Name.Trim().Length > 100)
            {
                AddError(field, "Attribute group name is required and must be 100 characters or fewer.");
            }
            if (group.DisplayOrder < 0)
            {
                AddError(field, "Attribute group order cannot be negative.");
            }
            if (group.Id is Guid groupId && (product is null || product.AttributeGroups.All(existing => existing.Id != groupId)))
            {
                AddError(field, "Attribute group does not belong to this product.");
            }

            var options = group.Options?.Where(option => option is not null).ToList() ?? [];
            if (group.Options?.Any(option => option is null) == true)
            {
                AddError(field, "Choices cannot contain empty entries.");
            }
            if (options.Count is 0 or > 100)
            {
                AddError(field, "Add between 1 and 100 choices to each attribute group.");
            }
            if (request.IsPublished && group.IsRequired && options.All(option => !option.IsActive))
            {
                AddError(field, "A required group on a published product needs an active choice.");
            }
            if (options.Where(option => option.Id.HasValue).Select(option => option.Id).Distinct().Count() != options.Count(option => option.Id.HasValue))
            {
                AddError(field, "A choice can only appear once in its attribute group.");
            }
            var optionNames = options.Select(option => option.Name?.Trim() ?? string.Empty).ToArray();
            if (optionNames.Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != optionNames.Count(value => value.Length > 0))
            {
                AddError(field, "Choice names must be unique within an attribute group.");
            }

            for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
            {
                var option = options[optionIndex];
                var optionField = $"{field}.Options[{optionIndex}]";
                if (string.IsNullOrWhiteSpace(option.Name) || option.Name.Trim().Length > 150)
                {
                    AddError(optionField, "Choice name is required and must be 150 characters or fewer.");
                }
                if (option.PriceAdjustment < 0 || option.PriceAdjustment > 99999999999999.9999m
                    || decimal.Round(option.PriceAdjustment, 4) != option.PriceAdjustment)
                {
                    AddError(optionField, "Price adjustment must be non-negative and support up to four decimal places.");
                }
                if (option.DisplayOrder < 0)
                {
                    AddError(optionField, "Choice order cannot be negative.");
                }
                if (option.Id is Guid optionId
                    && (product is null || group.Id is not Guid parentGroupId
                        || product.AttributeGroups.All(existingGroup => existingGroup.Id != parentGroupId
                            || existingGroup.Options.All(existingOption => existingOption.Id != optionId))))
                {
                    AddError(optionField, "Choice does not belong to this attribute group.");
                }
            }
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
            .ToArray(),
        product.AttributeGroups
            .OrderBy(group => group.DisplayOrder)
            .Select(group => new AdminProductAttributeGroupResponse(
                group.Id,
                group.Name,
                group.IsRequired,
                group.AllowMultiple,
                group.DisplayOrder,
                group.Options.OrderBy(option => option.DisplayOrder)
                    .Select(option => new AdminProductAttributeOptionResponse(
                        option.Id,
                        option.Name,
                        option.PriceAdjustment,
                        option.IsActive,
                        option.DisplayOrder))
                    .ToArray()))
            .ToArray(),
        new AdminFlashSaleRequest(
            product.FlashSaleEnabled,
            product.FlashSaleLabel,
            product.FlashSaleDiscountType,
            product.FlashSaleDiscountValue,
            product.FlashSaleStartsAtUtc,
            product.FlashSaleEndsAtUtc),
        product.SocialSharingEnabled,
        product.WhatsAppEnabled,
        product.WhatsAppPhoneNumber,
        product.WhatsAppPrefillMessage);

    private static object CreateAuditSnapshot(StoreProduct product) => new
    {
        product.Name,
        product.Slug,
        product.Description,
        product.IsPublished,
        FlashSale = new
        {
            product.FlashSaleEnabled,
            product.FlashSaleLabel,
            product.FlashSaleDiscountType,
            product.FlashSaleDiscountValue,
            product.FlashSaleStartsAtUtc,
            product.FlashSaleEndsAtUtc
        },
        product.SocialSharingEnabled,
        product.WhatsAppEnabled,
        product.WhatsAppPhoneNumber,
        product.WhatsAppPrefillMessage,
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
            .ToArray(),
        AttributeGroups = product.AttributeGroups
            .OrderBy(group => group.DisplayOrder)
            .Select(group => new
            {
                group.Name,
                group.IsRequired,
                group.AllowMultiple,
                Options = group.Options.OrderBy(option => option.DisplayOrder)
                    .Select(option => new { option.Name, option.PriceAdjustment, option.IsActive })
                    .ToArray()
            })
            .ToArray()
    };
}

public sealed record AdminProductRequest(
    string Name,
    string Slug,
    string Description,
    bool IsPublished,
    IReadOnlyList<AdminProductVariantRequest> Variants,
    IReadOnlyList<AdminProductAttributeGroupRequest>? AttributeGroups = null,
    AdminFlashSaleRequest? FlashSale = null,
    bool SocialSharingEnabled = false,
    bool WhatsAppEnabled = false,
    string? WhatsAppPhoneNumber = null,
    string? WhatsAppPrefillMessage = null);

public sealed record AdminFlashSaleRequest(
    bool Enabled,
    string? Label,
    string? DiscountType,
    decimal DiscountValue,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc)
{
    public static AdminFlashSaleRequest Disabled { get; } = new(false, "", "percentage", 0, null, null);
}

public sealed record AdminProductVariantRequest(
    Guid? Id,
    string Sku,
    string DisplayName,
    decimal Price,
    string CurrencyCode,
    int StockQuantity,
    bool IsActive);

public sealed record AdminProductAttributeGroupRequest(
    Guid? Id,
    string Name,
    bool IsRequired,
    bool AllowMultiple,
    int DisplayOrder,
    IReadOnlyList<AdminProductAttributeOptionRequest>? Options);

public sealed record AdminProductAttributeOptionRequest(
    Guid? Id,
    string Name,
    decimal PriceAdjustment,
    bool IsActive,
    int DisplayOrder);

public sealed record AdminProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    bool IsPublished,
    IReadOnlyList<AdminProductVariantResponse> Variants,
    IReadOnlyList<AdminProductAttributeGroupResponse> AttributeGroups,
    AdminFlashSaleRequest FlashSale,
    bool SocialSharingEnabled,
    bool WhatsAppEnabled,
    string WhatsAppPhoneNumber,
    string WhatsAppPrefillMessage);

public sealed record AdminProductVariantResponse(
    Guid Id,
    string Sku,
    string DisplayName,
    decimal Price,
    string CurrencyCode,
    int StockQuantity,
    bool IsActive);

public sealed record AdminProductAttributeGroupResponse(
    Guid Id,
    string Name,
    bool IsRequired,
    bool AllowMultiple,
    int DisplayOrder,
    IReadOnlyList<AdminProductAttributeOptionResponse> Options);

public sealed record AdminProductAttributeOptionResponse(
    Guid Id,
    string Name,
    decimal PriceAdjustment,
    bool IsActive,
    int DisplayOrder);

public sealed record AdminCatalogSaveResult(
    AdminProductResponse? Product,
    Dictionary<string, string[]>? Errors)
{
    public static AdminCatalogSaveResult Saved(AdminProductResponse product) => new(product, null);

    public static AdminCatalogSaveResult Invalid(Dictionary<string, string[]> errors) => new(null, errors);
}
