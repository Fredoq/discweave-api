using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Api.Tests;

internal sealed class FakeExternalMetadataProvider : IExternalMetadataProvider
{
    public string ProviderName => "discogs";

    public ExternalMetadataReleaseSearchQuery? LastReleaseSearchQuery { get; private set; }

    public ExternalMetadataLookupQuery? LastReleaseLookupQuery { get; private set; }

    public ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> ReleaseSearchResult { get; set; } =
        new(new ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>([], 0));

    public ExternalMetadataResult<ExternalMetadataReleaseDetail> ReleaseDetailResult { get; set; } =
        new(new ExternalMetadataError(
            ExternalMetadataErrorKind.Unavailable,
            "external_metadata.unavailable",
            "External metadata provider is unavailable"));

    public Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>> SearchReleasesAsync(
        ExternalMetadataReleaseSearchQuery query,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        LastReleaseSearchQuery = query;

        return Task.FromResult(ReleaseSearchResult);
    }

    public Task<ExternalMetadataResult<ExternalMetadataReleaseDetail>> GetReleaseAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        LastReleaseLookupQuery = query;

        return Task.FromResult(ReleaseDetailResult);
    }

    public Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>> SearchArtistsAsync(
        ExternalMetadataArtistSearchQuery query,
        CancellationToken cancellationToken)
    {
        _ = query;
        _ = cancellationToken;

        return Task.FromResult(new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>(
            new ExternalMetadataError(
                ExternalMetadataErrorKind.Unavailable,
                "external_metadata.unavailable",
                "External metadata provider is unavailable")));
    }

    public Task<ExternalMetadataResult<ExternalMetadataArtistDetail>> GetArtistAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken)
    {
        _ = query;
        _ = cancellationToken;

        return Task.FromResult(new ExternalMetadataResult<ExternalMetadataArtistDetail>(
            new ExternalMetadataError(
                ExternalMetadataErrorKind.Unavailable,
                "external_metadata.unavailable",
                "External metadata provider is unavailable")));
    }

    public Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>> SearchTracksAsync(
        ExternalMetadataTrackSearchQuery query,
        CancellationToken cancellationToken)
    {
        _ = query;
        _ = cancellationToken;

        return Task.FromResult(new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>(
            new ExternalMetadataError(
                ExternalMetadataErrorKind.Unavailable,
                "external_metadata.unavailable",
                "External metadata provider is unavailable")));
    }
}
