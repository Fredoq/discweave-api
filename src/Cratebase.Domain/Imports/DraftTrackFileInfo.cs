using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Imports;

public sealed record DraftTrackFileInfo
{
    public DraftTrackFileInfo(
        string filePath,
        string relativePath,
        AudioFileFormat format,
        long sizeBytes,
        DateTimeOffset lastModifiedAt,
        IOptionalValue<string> contentHash)
    {
        ArgumentNullException.ThrowIfNull(contentHash);

        FilePath = filePath;
        RelativePath = relativePath;
        Format = format;
        SizeBytes = sizeBytes;
        LastModifiedAt = lastModifiedAt;
        ContentHash = contentHash;
    }

    public string FilePath { get; }

    public string RelativePath { get; }

    public AudioFileFormat Format { get; }

    public long SizeBytes { get; }

    public DateTimeOffset LastModifiedAt { get; }

    public IOptionalValue<string> ContentHash { get; }
}
