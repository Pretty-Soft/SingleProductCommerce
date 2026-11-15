using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public sealed class StorefrontSettingsService(StoreDbContext database)
{
    public async Task<StorefrontSettingsResponse?> GetAsync(CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking()
            .Include(item => item.TrustItems)
            .Include(item => item.Images)
            .SingleOrDefaultAsync(cancellationToken);
        return product is null ? null : ToResponse(product);
    }

    public async Task<StorefrontSettingsSaveResult> SaveAsync(
        StorefrontSettingsRequest request,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .Include(item => item.Variants)
            .Include(item => item.TrustItems)
            .Include(item => item.Images)
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return StorefrontSettingsSaveResult.NotFound();

        var theme = request.Theme ?? StorefrontThemeRequest.Default;
        request = request with { Theme = theme };
        var errors = Validate(request, product);
        if (errors.Count > 0) return StorefrontSettingsSaveResult.Invalid(errors);

        var before = ToResponse(product);
        product.BrandName = request.BrandName.Trim();
        product.StorefrontHeadline = request.StorefrontHeadline?.Trim() ?? string.Empty;
        product.StorefrontSubtitle = request.StorefrontSubtitle?.Trim() ?? string.Empty;
        product.ThemePrimaryColor = request.Theme.PrimaryColor;
        product.ThemeAccentColor = request.Theme.AccentColor;
        product.ThemeTextColor = request.Theme.TextColor;
        product.ThemeMutedTextColor = request.Theme.MutedTextColor;
        product.ThemeBackgroundColor = request.Theme.BackgroundColor;
        product.ThemeSurfaceColor = request.Theme.SurfaceColor;
        product.ThemeBorderColor = request.Theme.BorderColor;
        product.ThemeProductTitleFontSizePx = request.Theme.ProductTitleFontSizePx;
        product.ThemeDetailsFontSizePx = request.Theme.DetailsFontSizePx;
        product.StorefrontTranslationsJson = CatalogTranslations.Serialize(request.StorefrontTranslations);
        product.FlashSaleEnabled = request.FlashSale.Enabled;
        product.FlashSaleLabel = request.FlashSale.Label?.Trim() ?? string.Empty;
        product.FlashSaleDiscountType = request.FlashSale.DiscountType?.Trim() ?? "percentage";
        product.FlashSaleDiscountValue = request.FlashSale.DiscountValue;
        product.FlashSaleStartsAtUtc = request.FlashSale.StartsAtUtc?.ToUniversalTime();
        product.FlashSaleEndsAtUtc = request.FlashSale.EndsAtUtc?.ToUniversalTime();
        product.SocialSharingEnabled = request.SocialSharingEnabled;
        product.WhatsAppEnabled = request.WhatsAppEnabled;
        product.WhatsAppPhoneNumber = request.WhatsAppPhoneNumber?.Trim() ?? string.Empty;
        product.WhatsAppPrefillMessage = request.WhatsAppPrefillMessage?.Trim() ?? string.Empty;
        product.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var existingItems = product.TrustItems.ToDictionary(item => item.Id);
        var retainedIds = new HashSet<Guid>();
        foreach (var requested in request.TrustItems ?? [])
        {
            ProductTrustItem item;
            if (requested.Id is Guid id)
            {
                item = existingItems[id];
                retainedIds.Add(id);
            }
            else
            {
                item = new ProductTrustItem { Id = Guid.NewGuid(), Product = product };
                product.TrustItems.Add(item);
            }

            item.Title = requested.Title.Trim();
            item.Description = requested.Description?.Trim() ?? string.Empty;
            item.IsActive = requested.IsActive;
            item.DisplayOrder = requested.DisplayOrder;
        }
        foreach (var removed in existingItems.Values.Where(item => !retainedIds.Contains(item.Id)))
        {
            database.ProductTrustItems.Remove(removed);
            product.TrustItems.Remove(removed);
        }

        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            Action = "storefront.settings.update",
            TargetId = product.Id,
            Target = product.Slug,
            Details = JsonSerializer.Serialize(new { before, after = ToResponse(product) }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return StorefrontSettingsSaveResult.Saved(ToResponse(product));
    }

    private static Dictionary<string, string[]> Validate(StorefrontSettingsRequest request, StoreProduct product)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (field, messages) in CatalogTranslations.Validate(request.StorefrontTranslations, nameof(request.StorefrontTranslations)))
            foreach (var message in messages) errors[field] = [message];
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }

        if (string.IsNullOrWhiteSpace(request.BrandName) || request.BrandName.Trim().Length > 120)
            Add(nameof(request.BrandName), "Brand name is required and must be 120 characters or fewer.");
        if ((request.StorefrontHeadline?.Length ?? 0) > 200)
            Add(nameof(request.StorefrontHeadline), "Headline must be 200 characters or fewer.");
        if ((request.StorefrontSubtitle?.Length ?? 0) > 500)
            Add(nameof(request.StorefrontSubtitle), "Subtitle must be 500 characters or fewer.");

        if (request.Theme is null)
        {
            Add(nameof(request.Theme), "Theme settings are required.");
        }
        else
        {
            foreach (var (field, value) in new[]
            {
                (nameof(request.Theme.PrimaryColor), request.Theme.PrimaryColor),
                (nameof(request.Theme.AccentColor), request.Theme.AccentColor),
                (nameof(request.Theme.TextColor), request.Theme.TextColor),
                (nameof(request.Theme.MutedTextColor), request.Theme.MutedTextColor),
                (nameof(request.Theme.BackgroundColor), request.Theme.BackgroundColor),
                (nameof(request.Theme.SurfaceColor), request.Theme.SurfaceColor),
                (nameof(request.Theme.BorderColor), request.Theme.BorderColor)
            })
                if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, "^#[0-9A-Fa-f]{6}$"))
                    Add($"Theme.{field}", "Choose a valid six-digit color.");
            if (request.Theme.ProductTitleFontSizePx is < 14 or > 16)
                Add("Theme.ProductTitleFontSizePx", "Product title size must be between 14 and 16 pixels.");
            if (request.Theme.DetailsFontSizePx is < 12 or > 14)
                Add("Theme.DetailsFontSizePx", "Details size must be between 12 and 14 pixels.");
        }

        if (request.FlashSale is null)
        {
            Add(nameof(request.FlashSale), "Flash-sale settings are required.");
        }
        else if (request.FlashSale.Enabled)
        {
            var sale = request.FlashSale;
            if (string.IsNullOrWhiteSpace(sale.Label) || sale.Label.Trim().Length > 100)
                Add(nameof(request.FlashSale), "A flash sale needs a label up to 100 characters.");
            if (sale.DiscountType is not ("percentage" or "fixedAmount"))
                Add(nameof(request.FlashSale), "Choose a percentage or fixed amount discount.");
            if (sale.DiscountType == "percentage" && (sale.DiscountValue <= 0 || sale.DiscountValue > 100))
                Add(nameof(request.FlashSale), "Percentage discount must be greater than 0 and no more than 100.");
            if (sale.DiscountType == "fixedAmount" && (sale.DiscountValue <= 0 || product.Variants.Where(item => item.IsActive).Any(item => item.Price <= sale.DiscountValue)))
                Add(nameof(request.FlashSale), "Fixed discount must be positive and below every active variant price.");
            if (sale.StartsAtUtc is null || sale.EndsAtUtc is null || sale.EndsAtUtc <= sale.StartsAtUtc)
                Add(nameof(request.FlashSale), "Set a valid sale start and end time; the end must be later than the start.");
        }

        var phone = request.WhatsAppPhoneNumber?.Trim() ?? string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (request.WhatsAppEnabled && (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[0-9\s()\-]*$") || digits.Length is < 7 or > 15))
            Add(nameof(request.WhatsAppPhoneNumber), "Enter a WhatsApp number with 7 to 15 digits, including country code.");
        if ((request.WhatsAppPhoneNumber?.Length ?? 0) > 20)
            Add(nameof(request.WhatsAppPhoneNumber), "WhatsApp phone number must be 20 characters or fewer.");
        if ((request.WhatsAppPrefillMessage?.Length ?? 0) > 500)
            Add(nameof(request.WhatsAppPrefillMessage), "WhatsApp message must be 500 characters or fewer.");

        var trustItems = request.TrustItems ?? [];
        if (trustItems.Count > 8) Add(nameof(request.TrustItems), "Add no more than 8 trust or service details.");
        if (trustItems.Select(item => item.Id).Where(id => id.HasValue).Distinct().Count() != trustItems.Count(item => item.Id.HasValue))
            Add(nameof(request.TrustItems), "A trust item can only appear once.");
        for (var index = 0; index < trustItems.Count; index++)
        {
            var item = trustItems[index];
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 80)
                Add($"TrustItems[{index}]", "A trust item title is required and must be 80 characters or fewer.");
            if ((item.Description?.Length ?? 0) > 300 || item.DisplayOrder < 0)
                Add($"TrustItems[{index}]", "Trust item descriptions must be up to 300 characters and order cannot be negative.");
            if (item.Id is Guid id && product.TrustItems.All(existing => existing.Id != id))
                Add($"TrustItems[{index}]", "Trust item does not belong to this product.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static StorefrontSettingsResponse ToResponse(StoreProduct product) => new(
        product.BrandName,
        product.StorefrontHeadline,
        product.StorefrontSubtitle,
        CatalogTranslations.Parse(product.StorefrontTranslationsJson),
        new AdminFlashSaleRequest(product.FlashSaleEnabled, product.FlashSaleLabel, product.FlashSaleDiscountType,
            product.FlashSaleDiscountValue, product.FlashSaleStartsAtUtc, product.FlashSaleEndsAtUtc),
        product.SocialSharingEnabled,
        product.WhatsAppEnabled,
        product.WhatsAppPhoneNumber,
        product.WhatsAppPrefillMessage,
        product.Images.Where(image => image.MediaKind == "logo")
            .Select(image => new ProductImageResponse(image.Id, "/api/v1/store/product/images/" + image.Id, image.MediaKind, image.AltText, image.DisplayOrder)).SingleOrDefault(),
        product.TrustItems.OrderBy(item => item.DisplayOrder)
            .Select(item => new AdminProductTrustItemResponse(item.Id, item.Title, item.Description, item.IsActive, item.DisplayOrder)).ToArray(),
        new StorefrontThemeRequest(product.ThemePrimaryColor, product.ThemeAccentColor, product.ThemeTextColor,
            product.ThemeMutedTextColor, product.ThemeBackgroundColor, product.ThemeSurfaceColor, product.ThemeBorderColor,
            product.ThemeProductTitleFontSizePx, product.ThemeDetailsFontSizePx));
}

public sealed record StorefrontSettingsRequest(
    string BrandName,
    string? StorefrontHeadline,
    string? StorefrontSubtitle,
    AdminFlashSaleRequest FlashSale,
    bool SocialSharingEnabled,
    bool WhatsAppEnabled,
    string? WhatsAppPhoneNumber,
    string? WhatsAppPrefillMessage,
    IReadOnlyList<AdminProductTrustItemRequest> TrustItems,
    Dictionary<string, Dictionary<string, string>>? StorefrontTranslations = null,
    StorefrontThemeRequest? Theme = null);

public sealed record StorefrontThemeRequest(
    string PrimaryColor, string AccentColor, string TextColor, string MutedTextColor,
    string BackgroundColor, string SurfaceColor, string BorderColor,
    int ProductTitleFontSizePx, int DetailsFontSizePx)
{
    public static StorefrontThemeRequest Default { get; } = new(
        "#242020", "#c17b88", "#282326", "#777078", "#fbf8f8", "#ffffff", "#eee5e8", 15, 13);
}

public sealed record StorefrontSettingsResponse(
    string BrandName,
    string StorefrontHeadline,
    string StorefrontSubtitle,
    Dictionary<string, Dictionary<string, string>> StorefrontTranslations,
    AdminFlashSaleRequest FlashSale,
    bool SocialSharingEnabled,
    bool WhatsAppEnabled,
    string WhatsAppPhoneNumber,
    string WhatsAppPrefillMessage,
    ProductImageResponse? BrandLogo,
    IReadOnlyList<AdminProductTrustItemResponse> TrustItems,
    StorefrontThemeRequest Theme);

public sealed record StorefrontSettingsSaveResult(StorefrontSettingsResponse? Settings, Dictionary<string, string[]>? Errors, bool IsNotFound)
{
    public static StorefrontSettingsSaveResult Saved(StorefrontSettingsResponse settings) => new(settings, null, false);
    public static StorefrontSettingsSaveResult Invalid(Dictionary<string, string[]> errors) => new(null, errors, false);
    public static StorefrontSettingsSaveResult NotFound() => new(null, null, true);
}
