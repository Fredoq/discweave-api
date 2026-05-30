using DiscWeave.Domain.Imports;

namespace DiscWeave.Importing;

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
