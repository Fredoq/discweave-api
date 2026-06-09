using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Api.Features.ExternalMetadata;

public sealed record ExternalMetadataArtistCandidateResponse(
    ExternalMetadataSource Source,
    string Name,
    string? Profile,
    IReadOnlyList<string> NameVariations);

public sealed record ExternalMetadataArtistDetailResponse(
    ExternalMetadataSource Source,
    string Name,
    string? Profile,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Members,
    IReadOnlyList<string> NameVariations,
    ExternalMetadataArtistDraftResponse Draft);

public sealed record ExternalMetadataArtistDraftResponse(
    string Name,
    IReadOnlyList<ExternalMetadataDraftExternalSourceResponse> ExternalSources);
