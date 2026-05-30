using DiscWeave.Application.Catalog.Releases;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Tests;

public sealed class FileSystemReleaseCoverStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "discweave-release-cover-storage-tests",
        Guid.NewGuid().ToString("N"));

    [Fact(DisplayName = "Release cover storage saves generated keys and can open and delete files")]
    public async Task Release_cover_storage_saves_generated_keys_and_can_open_and_delete_files()
    {
        byte[] content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
        var collectionId = CollectionId.New();
        var releaseId = ReleaseId.New();
        FileSystemReleaseCoverStorage storage = CreateStorage();

        ReleaseCoverStoredFile storedFile = await storage.SaveAsync(
            collectionId,
            releaseId,
            ".png",
            new MemoryStream(content),
            CancellationToken.None);
        string[] files = Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories);
        await using Stream? openedStream = await storage.TryOpenReadAsync(storedFile.StorageKey, CancellationToken.None);
        await storage.DeleteAsync(storedFile.StorageKey, CancellationToken.None);

        Assert.EndsWith(".png", storedFile.StorageKey, StringComparison.Ordinal);
        Assert.Contains(collectionId.Value.ToString("N"), storedFile.StorageKey, StringComparison.Ordinal);
        Assert.Contains(releaseId.Value.ToString("N"), storedFile.StorageKey, StringComparison.Ordinal);
        _ = Assert.Single(files);
        Assert.NotNull(openedStream);
        using var copy = new MemoryStream();
        await openedStream.CopyToAsync(copy);
        Assert.Equal(content, copy.ToArray());
        Assert.False(File.Exists(files[0]));
    }

    [Theory(DisplayName = "Release cover storage rejects unsafe keys")]
    [InlineData("../escape.png")]
    [InlineData("collection/../../escape.png")]
    [InlineData("/absolute.png")]
    public async Task Release_cover_storage_rejects_unsafe_keys(string storageKey)
    {
        FileSystemReleaseCoverStorage storage = CreateStorage();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.TryOpenReadAsync(storageKey, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private FileSystemReleaseCoverStorage CreateStorage()
    {
        return new FileSystemReleaseCoverStorage(Options.Create(new ReleaseCoverStorageOptions
        {
            StorageRoot = _rootPath
        }));
    }
}
