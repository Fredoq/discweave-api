namespace DiscWeave.Api.Features.Imports;

public sealed record ReleaseImportDraftTrackResponse(
    Guid Id,
    string FilePath,
    string RelativePath,
    string Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    int? DurationSeconds,
    int? Position,
    string Title,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ReleaseImportArtistCreditResponse> ArtistCredits,
    IReadOnlyList<EntitySuggestionResponse> ArtistSuggestions,
    IReadOnlyList<EntitySuggestionResponse> TrackSuggestions,
    bool IsSkipped,
    Guid? SelectedTrackId,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<ImportIssueResponse> Issues);
