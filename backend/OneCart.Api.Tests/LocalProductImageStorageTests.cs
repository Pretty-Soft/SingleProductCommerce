using OneCart.Api.Catalog;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class LocalProductImageStorageTests
{
    [Fact]
    public async Task Saves_reads_and_deletes_images_inside_the_configured_local_folder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "onecart-image-storage-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalProductImageStorage(rootPath);
        var storageKey = Guid.NewGuid().ToString("N");
        byte[] image = [137, 80, 78, 71, 13, 10, 26, 10];

        try
        {
            await storage.SaveAsync(storageKey, image, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(rootPath, storageKey)));

            await using (var stream = await storage.OpenReadAsync(storageKey, CancellationToken.None))
            {
                Assert.NotNull(stream);
                using var copy = new MemoryStream();
                await stream!.CopyToAsync(copy);
                Assert.Equal(image, copy.ToArray());
            }

            await storage.DeleteAsync(storageKey, CancellationToken.None);
            Assert.False(File.Exists(Path.Combine(rootPath, storageKey)));
            Assert.Null(await storage.OpenReadAsync(storageKey, CancellationToken.None));
        }
        finally
        {
            var fullRootPath = Path.GetFullPath(rootPath);
            var fullTempPath = Path.GetFullPath(Path.GetTempPath());
            if (Directory.Exists(fullRootPath)
                && fullRootPath.StartsWith(fullTempPath, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(fullRootPath).StartsWith("onecart-image-storage-", StringComparison.Ordinal))
            {
                Directory.Delete(fullRootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Rejects_storage_keys_that_could_escape_the_local_folder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "onecart-image-storage-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalProductImageStorage(rootPath);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.OpenReadAsync("..\\outside", CancellationToken.None));
    }
}
