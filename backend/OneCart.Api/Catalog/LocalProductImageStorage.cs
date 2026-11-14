namespace OneCart.Api.Catalog;

public sealed class LocalProductImageStorage(string rootPath) : IProductImageStorage
{
    public async Task SaveAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = GetPath(storageKey);
        Directory.CreateDirectory(rootPath);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await file.WriteAsync(content, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string storageKey)
    {
        if (!Guid.TryParseExact(storageKey, "N", out _))
        {
            throw new InvalidOperationException("Invalid product media storage key.");
        }

        return Path.Combine(rootPath, storageKey);
    }
}
