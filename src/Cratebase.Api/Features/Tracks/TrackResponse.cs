namespace Cratebase.Api.Features.Tracks;

public sealed record TrackResponse(
    Guid Id,
    string Title,
    int? DurationSeconds,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<TrackCreditResponse> Credits,
    IReadOnlyList<TrackReleaseAppearanceResponse> ReleaseAppearances);

public sealed record TrackCreditResponse(Guid ArtistId, string ArtistName, string Role);

public sealed record TrackReleaseAppearanceResponse(
    Guid ReleaseId,
    string ReleaseTitle,
    string ReleaseArtist,
    int? Year,
    string? Label,
    int Position,
    int? DurationSeconds,
    string? VersionNote);
