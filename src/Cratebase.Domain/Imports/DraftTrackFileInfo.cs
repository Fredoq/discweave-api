using Cratebase.Domain.Collection;

namespace Cratebase.Domain.Imports;

public sealed record DraftTrackFileInfo(
    string FilePath,
    string RelativePath,
    AudioFileFormat Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt);
