namespace Cratebase.Domain.Imports;

public sealed record ReleaseImportDraftEditableFields(
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
    IReadOnlyList<ReleaseImportArtistCredit> ArtistCredits,
    IReadOnlyList<ReleaseImportLabel> Labels,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ImportReviewIssue> Issues);
