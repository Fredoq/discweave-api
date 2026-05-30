namespace DiscWeave.Domain.Imports;

public sealed record ParsedReleaseFolder(
    string? CatalogNumber,
    DateOnly? ReleaseDate,
    int? Year,
    IReadOnlyList<string> ArtistNames,
    bool IsVariousArtists,
    string? Title,
    IReadOnlyList<ImportReviewIssue> Issues,
    string? MatchedTemplate);
