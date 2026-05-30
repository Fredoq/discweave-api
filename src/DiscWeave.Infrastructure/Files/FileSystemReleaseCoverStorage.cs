using DiscWeave.Application.Catalog.Releases;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Files;

public sealed class FileSystemReleaseCoverStorage : IReleaseCoverStorage
{
    private readonly string _rootPath;

    public FileSystemReleaseCoverStorage(IOptions<ReleaseCoverStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? configuredRootPath = options.Value.StorageRoot;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "release-covers")
            : configuredRootPath);
    }

    public async Task<ReleaseCoverStoredFile> SaveAsync(
        CollectionId collectionId,
        ReleaseId releaseId,
        string extension,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        string normalizedExtension = NormalizeExtension(extension);
        string storageKey = string.Join(
            '/',
            collectionId.Value.ToString("N"),
            releaseId.Value.ToString("N"),
            $"{Guid.CreateVersion7():N}{normalizedExtension}");
        string targetPath = ResolvePath(storageKey);
        string? directoryPath = Path.GetDirectoryName(targetPath);
        if (directoryPath is not null)
        {
            _ = Directory.CreateDirectory(directoryPath);
        }

        await using var fileStream = new FileStream(
            targetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(fileStream, cancellationToken);

        return new ReleaseCoverStoredFile(storageKey);
    }

    public Task<Stream?> TryOpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string targetPath = ResolvePath(storageKey);
        Stream? stream = File.Exists(targetPath)
            ? new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string targetPath = ResolvePath(storageKey);
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.StartsWith('/') ||
            storageKey.StartsWith('\\'))
        {
            throw new InvalidOperationException("Release cover storage key is invalid");
        }

        string[] segments = storageKey.Split('/');
        if (segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) ||
                segment == "." ||
                segment == ".." ||
                segment.Contains('\\')))
        {
            throw new InvalidOperationException("Release cover storage key is invalid");
        }

        string targetPath = Path.GetFullPath(Path.Combine([_rootPath, .. segments]));
        string rootPathWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        return !targetPath.StartsWith(rootPathWithSeparator, StringComparison.Ordinal)
            ? throw new InvalidOperationException("Release cover storage key is invalid")
            : targetPath;
    }

    private static string NormalizeExtension(string extension)
    {
        string value = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim();
        string normalizedExtension = value.StartsWith('.') ? value : $".{value}";
        return normalizedExtension.Length == 1 ||
            normalizedExtension.Contains('/') ||
            normalizedExtension.Contains('\\') ||
            normalizedExtension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            ? throw new InvalidOperationException("Release cover extension is invalid")
            : normalizedExtension.ToLowerInvariant();
    }
}
