using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;

namespace Cratebase.Importing;

public sealed record ReleaseFolderScanPayload(
    string SourceRoot,
    IReadOnlyList<ReleaseFolderScanDraft> Drafts,
    int IgnoredFileCount);

public sealed record ReleaseFolderScanDraft(
    string SourcePath,
    string RelativePath,
    string Title,
    string Type,
    string? CatalogNumber,
    string? LabelName,
    DateOnly? ReleaseDate,
    int? Year,
    bool IsVariousArtists,
    bool NotOnLabel,
    string? CoverPath,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ImportReviewIssue> Issues,
    CoverArtifactPayload? CoverArtifact,
    IReadOnlyList<ReleaseFolderScanTrack> Tracks);

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

public sealed record CoverArtifactPayload(
    string SourcePath,
    string FileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    string ContentBase64);
