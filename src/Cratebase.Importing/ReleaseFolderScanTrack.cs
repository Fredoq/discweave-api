using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;

namespace Cratebase.Importing;

public sealed record ReleaseFolderScanTrack(
    string FilePath,
    string RelativePath,
    AudioFileFormat Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    TimeSpan? Duration,
    int? Position,
    string Title,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ImportReviewIssue> Issues);
