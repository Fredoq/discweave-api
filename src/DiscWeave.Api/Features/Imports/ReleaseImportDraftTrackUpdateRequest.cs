namespace DiscWeave.Api.Features.Imports;

public sealed record ReleaseImportDraftTrackUpdateRequest(
    Guid Id,
    int? Position,
    string? Disc,
    string? Side,
    string Title,
    int? DurationSeconds,
    IReadOnlyList<string>? ArtistNames,
    IReadOnlyList<ReleaseImportArtistCreditRequest>? ArtistCredits,
    IReadOnlyList<Guid>? SelectedArtistIds,
    Guid? SelectedTrackId,
    bool IsSkipped);
