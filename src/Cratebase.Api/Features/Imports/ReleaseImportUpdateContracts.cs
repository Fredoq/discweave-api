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

public sealed record ReleaseImportDraftTrackUpdateRequest(
    Guid Id,
    int? Position,
    string Title,
    int? DurationSeconds,
    IReadOnlyList<string>? ArtistNames,
    IReadOnlyList<ReleaseImportArtistCreditRequest>? ArtistCredits,
    IReadOnlyList<Guid>? SelectedArtistIds,
    Guid? SelectedTrackId,
    bool IsSkipped);

public sealed record ReleaseImportArtistCreditRequest(Guid? ArtistId, string? Name, string? Role);

public sealed record ReleaseImportLabelRequest(Guid? LabelId, string? Name, string? CatalogNumber, bool HasNoCatalogNumber);
