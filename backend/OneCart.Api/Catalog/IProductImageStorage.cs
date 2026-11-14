namespace OneCart.Api.Catalog;

public interface IProductImageStorage
{
    Task SaveAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
