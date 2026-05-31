using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Releases;

public sealed record ReleaseResponse(
    Guid Id,
    string Title,
    string Type,
    Guid? LabelId,
    int? Year,
    string? ReleaseDate,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    bool IsVariousArtists,
    bool NotOnLabel,
    CoverImageResponse? CoverImage,
    IReadOnlyList<ExternalSourceReferenceResponse>? ExternalSources,
    IReadOnlyList<ReleaseArtistCreditResponse> ArtistCredits,
    IReadOnlyList<ReleaseLabelResponse> Labels,
    IReadOnlyList<ReleaseTracklistItemResponse> Tracklist);

public sealed record CoverImageResponse(
    string Url,
    string ContentType,
    string OriginalFileName,
    long SizeBytes,
    string SourceType);

public sealed record ReleaseArtistCreditResponse(Guid ArtistId, string ArtistName, string Role);

public sealed record ReleaseLabelResponse(Guid? LabelId, string Name, string? CatalogNumber, bool HasNoCatalogNumber);

public sealed record ReleaseTracklistItemResponse(
    Guid TrackId,
    string Title,
    int Position,
    int? DurationSeconds,
    IReadOnlyList<ReleaseArtistCreditResponse> ArtistCredits,
    string? VersionNote);
