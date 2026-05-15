using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Application.Catalog.Releases;

public interface IReleaseCoverStorage
{
    Task<ReleaseCoverStoredFile> SaveAsync(
        CollectionId collectionId,
        ReleaseId releaseId,
        string extension,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream?> TryOpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record ReleaseCoverStoredFile(string StorageKey);
