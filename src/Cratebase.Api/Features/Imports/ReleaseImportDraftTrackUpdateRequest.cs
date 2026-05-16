namespace Cratebase.Api.Features.Imports;

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
