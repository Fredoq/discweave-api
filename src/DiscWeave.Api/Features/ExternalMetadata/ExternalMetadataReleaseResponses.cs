using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Api.Features.ExternalMetadata;

public sealed record ExternalMetadataSearchResponse<T>(
    IReadOnlyList<T> Items,
    int Limit,
    int Total);

public sealed record ExternalMetadataReleaseCandidateResponse(
    ExternalMetadataSource Source,
    string Title,
    IReadOnlyList<string> Artists,
    int? Year,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Formats,
    string? CatalogNumber,
    IReadOnlyList<string> Barcodes);

public sealed record ExternalMetadataReleaseDetailResponse(
    ExternalMetadataSource Source,
    string Title,
    IReadOnlyList<string> Artists,
    int? Year,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Formats,
    IReadOnlyList<ExternalMetadataReleaseTrackResponse> Tracklist,
    IReadOnlyList<ExternalMetadataReleaseIdentifierResponse> Identifiers,
    IReadOnlyList<string> Barcodes,
    string? CatalogNumber,
    IReadOnlyList<ExternalMetadataReleaseCreditResponse> Credits,
    ExternalMetadataReleaseDraftResponse Draft);

public sealed record ExternalMetadataReleaseTrackResponse(
    string Title,
    string? Position,
    int? DurationSeconds,
    IReadOnlyList<string> Artists);

public sealed record ExternalMetadataReleaseIdentifierResponse(
    string Type,
    string Value);

public sealed record ExternalMetadataReleaseCreditResponse(
    string Name,
    string Role,
    string? TrackTitle,
    string? TrackPosition);

public sealed record ExternalMetadataReleaseDraftResponse(
    string Title,
    string? Type,
    IReadOnlyList<string> Genres,
    int? Year,
    string? ReleaseDate,
    IReadOnlyList<ExternalMetadataReleaseDraftArtistCreditResponse> ArtistCredits,
    IReadOnlyList<ExternalMetadataReleaseDraftLabelResponse> Labels,
    IReadOnlyList<ExternalMetadataReleaseDraftTrackResponse> Tracklist,
    IReadOnlyList<ExternalMetadataDraftExternalSourceResponse> ExternalSources);

public sealed record ExternalMetadataReleaseDraftArtistCreditResponse(
    string Name,
    string Role);

public sealed record ExternalMetadataReleaseDraftLabelResponse(
    string Name,
    string? CatalogNumber,
    bool HasNoCatalogNumber);

public sealed record ExternalMetadataReleaseDraftTrackResponse(
    string Title,
    int Position,
    int? DurationSeconds,
    IReadOnlyList<ExternalMetadataReleaseDraftArtistCreditResponse> ArtistCredits);

public sealed record ExternalMetadataDraftExternalSourceResponse(
    string ProviderName,
    string ResourceType,
    string ExternalId,
    string SourceUrl);
