using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Collection;

public sealed record FileImportIdentity
{
    private FileImportIdentity(FilePath path, long sizeBytes, DateTimeOffset lastModifiedAt, IOptionalValue<string> contentHash)
    {
        Path = path;
        SizeBytes = sizeBytes;
        LastModifiedAt = lastModifiedAt;
        ContentHash = contentHash;
    }

    public FilePath Path { get; }

    public long SizeBytes { get; }

    public DateTimeOffset LastModifiedAt { get; }

    public IOptionalValue<string> ContentHash { get; }

    public static FileImportIdentity Create(
        FilePath path,
        long sizeBytes,
        DateTimeOffset lastModifiedAt)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new FileImportIdentity(
            path,
            Guard.Positive(sizeBytes, nameof(sizeBytes), "file_import_identity.size_required"),
            lastModifiedAt,
            Optional.Missing<string>());
    }

    public static FileImportIdentity Create(
        FilePath path,
        long sizeBytes,
        DateTimeOffset lastModifiedAt,
        string contentHash)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(contentHash);

        return new FileImportIdentity(
            path,
            Guard.Positive(sizeBytes, nameof(sizeBytes), "file_import_identity.size_required"),
            lastModifiedAt,
            string.IsNullOrWhiteSpace(contentHash)
                ? Optional.Missing<string>()
                : Optional.From(contentHash.Trim().ToLowerInvariant()));
    }
}
