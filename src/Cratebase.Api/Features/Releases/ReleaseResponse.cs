namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseResponse(
    Guid Id,
    string Title,
    string Type,
    Guid? LabelId,
    int? Year,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    bool IsVariousArtists,
    bool NotOnLabel,
    IReadOnlyList<ReleaseArtistCreditResponse> ArtistCredits,
    IReadOnlyList<ReleaseLabelResponse> Labels,
    IReadOnlyList<ReleaseTracklistItemResponse> Tracklist);

public sealed record ReleaseArtistCreditResponse(Guid ArtistId, string ArtistName, string Role);

public sealed record ReleaseLabelResponse(Guid? LabelId, string Name, string? CatalogNumber, bool HasNoCatalogNumber);

public sealed record ReleaseTracklistItemResponse(
    Guid TrackId,
    string Title,
    int Position,
    int? DurationSeconds,
    IReadOnlyList<ReleaseArtistCreditResponse> ArtistCredits,
    string? VersionNote);
