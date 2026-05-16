namespace Cratebase.Domain.Imports;

public sealed record ParsedTrackFile(
    int? Position,
    string? Title,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ImportReviewIssue> Issues,
    string? MatchedTemplate);
