using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Api.Features.ExternalMetadata;

public sealed record ExternalMetadataTrackCandidateResponse(
    ExternalMetadataSource Source,
    string Title,
    string? Position,
    int? DurationSeconds,
    IReadOnlyList<string> Artists,
    ExternalMetadataTrackReleaseContextResponse Release);

public sealed record ExternalMetadataTrackDetailResponse(
    ExternalMetadataSource Source,
    string Title,
    string? Position,
    int? DurationSeconds,
    IReadOnlyList<string> Artists,
    IReadOnlyList<ExternalMetadataTrackCreditResponse> Credits,
    ExternalMetadataTrackReleaseContextResponse Release,
    ExternalMetadataTrackDraftResponse Draft);

public sealed record ExternalMetadataTrackReleaseContextResponse(
    ExternalMetadataSource Source,
    string Title,
    int? Year,
    IReadOnlyList<string> Artists);

public sealed record ExternalMetadataTrackCreditResponse(
    string Name,
    string Role);

public sealed record ExternalMetadataTrackDraftResponse(
    string Title,
    int? DurationSeconds,
    IReadOnlyList<ExternalMetadataReleaseDraftArtistCreditResponse> ArtistCredits,
    IReadOnlyList<ExternalMetadataDraftExternalSourceResponse> ExternalSources);
