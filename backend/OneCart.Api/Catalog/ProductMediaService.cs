using System.Text.Json;
using OneCart.Api.Admin;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;

namespace OneCart.Api.Catalog;

public sealed class ProductMediaService(StoreDbContext database, IProductImageStorage storage)
{
    public const int MaximumImageBytes = 10 * 1024 * 1024;
    private const int MaximumProductImages = 8;

    public async Task<ProductDeleteResult> DeleteProductAsync(
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var product = await database.Products
            .Include(candidate => candidate.Variants)
            .Include(candidate => candidate.Images)
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return ProductDeleteResult.NotFound;

        var variantIds = product.Variants.Select(variant => variant.Id).ToArray();
        if (variantIds.Length > 0 && await database.StoreOrderLines
            .AnyAsync(line => variantIds.Contains(line.VariantId), cancellationToken))
        {
            return ProductDeleteResult.HasOrderHistory;
        }

        var mediaKeys = product.Images.Select(image => image.StorageKey).ToArray();
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            Action = "catalog.product.delete",
            TargetId = product.Id,
            Target = "product",
            Details = JsonSerializer.Serialize(new { product.Name, product.Slug, VariantCount = product.Variants.Count, ImageCount = product.Images.Count }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        database.Products.Remove(product);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var mediaKey in mediaKeys)
        {
            await storage.DeleteAsync(mediaKey, CancellationToken.None);
        }

        return ProductDeleteResult.Deleted;
    }

    public async Task<ProductMediaOperationResult> UploadAsync(
        Stream source,
        long declaredLength,
        string mediaKind,
        string altText,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        if (declaredLength is <= 0 or > MaximumImageBytes)
        {
            return ProductMediaOperationResult.Invalid("Image must be between 1 byte and 10 MB.");
        }

        if (mediaKind is not ("product" or "logo"))
        {
            return ProductMediaOperationResult.Invalid("Choose product image or brand logo.");
        }

        var normalizedAlt = altText?.Trim() ?? string.Empty;
        if (normalizedAlt.Length is 0 or > 200)
        {
            return ProductMediaOperationResult.Invalid("Alternative text is required and must be 200 characters or fewer.");
        }

        var content = await ReadBoundedAsync(source, cancellationToken);
        if (content is null || content.LongLength != declaredLength)
        {
            return ProductMediaOperationResult.Invalid("Image must not exceed 10 MB.");
        }

        var contentType = DetectContentType(content);
        if (contentType is null)
        {
            return ProductMediaOperationResult.Invalid("Upload a valid JPEG, PNG, or WebP image.");
        }

        var product = await database.Products
            .Include(candidate => candidate.Images)
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return ProductMediaOperationResult.MissingProduct();
        }

        var existingLogos = product.Images.Where(image => image.MediaKind == "logo").ToArray();
        if (mediaKind == "product" && product.Images.Count(image => image.MediaKind == "product") >= MaximumProductImages)
        {
            return ProductMediaOperationResult.Invalid($"A product can have up to {MaximumProductImages} images.");
        }

        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = contentType,
            MediaKind = mediaKind,
            AltText = normalizedAlt,
            DisplayOrder = mediaKind == "logo"
                ? 0
                : product.Images.Where(item => item.MediaKind == "product").Select(item => item.DisplayOrder).DefaultIfEmpty(-1).Max() + 1,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await storage.SaveAsync(image.StorageKey, content, cancellationToken);
            foreach (var oldLogo in existingLogos)
            {
                database.ProductImages.Remove(oldLogo);
                product.Images.Remove(oldLogo);
            }
            database.ProductImages.Add(image);
            AddAudit(database, product.Id, actorId, actorEmail, "catalog.image.upload", image.Id,
                JsonSerializer.Serialize(new { mediaKind, contentType, bytes = content.Length }));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(image.StorageKey, CancellationToken.None);
            throw;
        }

        foreach (var oldLogo in existingLogos)
        {
            await storage.DeleteAsync(oldLogo.StorageKey, cancellationToken);
        }

        return ProductMediaOperationResult.Saved(ToResponse(image));
    }

    public async Task<ProductMediaOperationResult> UpdateAsync(
        Guid imageId,
        string altText,
        int displayOrder,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        var normalizedAlt = altText?.Trim() ?? string.Empty;
        if (normalizedAlt.Length is 0 or > 200 || displayOrder < 0)
        {
            return ProductMediaOperationResult.Invalid("Alternative text is required (up to 200 characters) and image order cannot be negative.");
        }

        var image = await database.ProductImages.SingleOrDefaultAsync(item => item.Id == imageId, cancellationToken);
        if (image is null) return ProductMediaOperationResult.NotFound();

        var before = new { image.AltText, image.DisplayOrder };
        image.AltText = normalizedAlt;
        image.DisplayOrder = image.MediaKind == "logo" ? 0 : displayOrder;
        AddAudit(database, image.ProductId, actorId, actorEmail, "catalog.image.update", image.Id,
            JsonSerializer.Serialize(new { before, after = new { image.AltText, image.DisplayOrder } }));
        await database.SaveChangesAsync(cancellationToken);
        return ProductMediaOperationResult.Saved(ToResponse(image));
    }

    public async Task<bool> DeleteAsync(
        Guid imageId,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        var image = await database.ProductImages.SingleOrDefaultAsync(item => item.Id == imageId, cancellationToken);
        if (image is null) return false;

        var storageKey = image.StorageKey;
        AddAudit(database, image.ProductId, actorId, actorEmail, "catalog.image.delete", image.Id,
            JsonSerializer.Serialize(new { image.MediaKind, image.ContentType }));
        database.ProductImages.Remove(image);
        await database.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(storageKey, cancellationToken);
        return true;
    }

    public async Task<ProductImageFile?> OpenPublishedImageAsync(Guid imageId, CancellationToken cancellationToken)
    {
        var image = await database.ProductImages
            .AsNoTracking()
            .Where(item => item.Id == imageId && item.Product.IsPublished)
            .Select(item => new { item.StorageKey, item.ContentType })
            .SingleOrDefaultAsync(cancellationToken);
        if (image is null) return null;

        var stream = await storage.OpenReadAsync(image.StorageKey, cancellationToken);
        return stream is null ? null : new ProductImageFile(stream, image.ContentType);
    }

    public async Task<ProductImageFile?> OpenAdminImageAsync(Guid imageId, CancellationToken cancellationToken)
    {
        var image = await database.ProductImages.AsNoTracking()
            .Where(item => item.Id == imageId)
            .Select(item => new { item.StorageKey, item.ContentType })
            .SingleOrDefaultAsync(cancellationToken);
        if (image is null) return null;

        var stream = await storage.OpenReadAsync(image.StorageKey, cancellationToken);
        return stream is null ? null : new ProductImageFile(stream, image.ContentType);
    }

    public Task<string?> GetMediaKindAsync(Guid imageId, CancellationToken cancellationToken) =>
        database.ProductImages.AsNoTracking()
            .Where(item => item.Id == imageId)
            .Select(item => item.MediaKind)
            .SingleOrDefaultAsync(cancellationToken);

    public static ProductImageResponse ToResponse(ProductImage image) => new(
        image.Id,
        "/api/v1/store/product/images/" + image.Id,
        image.MediaKind,
        image.AltText,
        image.DisplayOrder);

    private static async Task<byte[]?> ReadBoundedAsync(Stream source, CancellationToken cancellationToken)
    {
        await using var destination = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var count = await source.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            if (destination.Length + count > MaximumImageBytes) return null;
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        return destination.ToArray();
    }

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    private static void AddAudit(StoreDbContext database, Guid productId, Guid actorId, string actorEmail, string action, Guid imageId, string details)
    {
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            Action = action,
            TargetId = imageId,
            Target = productId.ToString(),
            Details = details,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}

public enum ProductDeleteResult
{
    Deleted,
    NotFound,
    HasOrderHistory
}

public sealed record ProductImageFile(Stream Content, string ContentType);

public sealed record ProductMediaOperationResult(ProductImageResponse? Image, string? Error, bool IsNotFound)
{
    public static ProductMediaOperationResult Saved(ProductImageResponse image) => new(image, null, false);
    public static ProductMediaOperationResult Invalid(string error) => new(null, error, false);
    public static ProductMediaOperationResult MissingProduct() => new(null, "Create a product draft before uploading media.", false);
    public static ProductMediaOperationResult NotFound() => new(null, null, true);
}
