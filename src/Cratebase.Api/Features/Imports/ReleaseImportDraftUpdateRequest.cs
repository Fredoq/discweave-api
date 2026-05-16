namespace Cratebase.Api.Features.Imports;

public sealed record ReleaseImportDraftUpdateRequest(
    string Title,
    string? Type,
    string? CatalogNumber,
    string? LabelName,
    string? ReleaseDate,
    int? Year,
    bool IsVariousArtists,
    bool NotOnLabel,
    IReadOnlyList<string>? ArtistNames,
    IReadOnlyList<ReleaseImportArtistCreditRequest>? ArtistCredits,
    IReadOnlyList<ReleaseImportLabelRequest>? Labels,
    IReadOnlyList<Guid>? SelectedArtistIds,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<string>? Tags,
    string? CoverPath,
    IReadOnlyList<ReleaseImportDraftTrackUpdateRequest>? Tracks);
