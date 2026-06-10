using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Tracks;

public sealed record TrackResponse(
    Guid Id,
    string Title,
    int? DurationSeconds,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ExternalSourceReferenceResponse> ExternalSources,
    IReadOnlyList<TrackCreditResponse> Credits,
    IReadOnlyList<TrackReleaseAppearanceResponse> ReleaseAppearances);

public sealed record TrackCreditResponse(Guid ArtistId, string ArtistName, string Role, IReadOnlyList<string> Roles);

public sealed record TrackReleaseAppearanceResponse(
    Guid ReleaseId,
    string ReleaseTitle,
    string ReleaseArtist,
    int? Year,
    string? Label,
    int Position,
    string? Disc,
    string? Side,
    int? DurationSeconds,
    string? VersionNote);
