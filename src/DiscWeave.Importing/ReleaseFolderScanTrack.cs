using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Imports;

namespace DiscWeave.Importing;

public sealed record ReleaseFolderScanTrack(
    string FilePath,
    string RelativePath,
    AudioFileFormat Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    string? ContentHash,
    TimeSpan? Duration,
    int? Position,
    string? Disc,
    string? Side,
    string Title,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ImportReviewIssue> Issues);
