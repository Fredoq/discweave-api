namespace Cratebase.Domain.Imports;

public sealed class ReleaseImportCoverArtifact : IEquatable<ReleaseImportCoverArtifact>
{
    private readonly byte[] _content;

    public ReleaseImportCoverArtifact(
        string fileName,
        string extension,
        string contentType,
        long sizeBytes,
        byte[] content)
    {
        FileName = fileName;
        Extension = extension;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        _content = [.. content];
    }

    public string FileName { get; }

    public string Extension { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public IReadOnlyList<byte> Content => [.. _content];

    public bool Equals(ReleaseImportCoverArtifact? other)
    {
        return other is not null &&
            FileName == other.FileName &&
            Extension == other.Extension &&
            ContentType == other.ContentType &&
            SizeBytes == other.SizeBytes &&
            _content.SequenceEqual(other._content);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ReleaseImportCoverArtifact);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FileName);
        hash.Add(Extension);
        hash.Add(ContentType);
        hash.Add(SizeBytes);
        foreach (byte value in _content)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
