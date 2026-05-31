namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider
{
#pragma warning disable CA1812
    private sealed record DiscogsSearchResponse(
        DiscogsPagination? Pagination,
        DiscogsSearchResult[] Results);

    private sealed record DiscogsPagination(int? Items);

    private sealed record DiscogsSearchResult(
        string? Type,
        long Id,
        string? Title,
        int? Year,
        string[]? Label,
        string[]? Format,
        string? Catno,
        string[]? Barcode,
        string? Uri);

    private sealed record DiscogsReleaseDetailResponse(
        long Id,
        string? Title,
        int? Year,
        string? Uri,
        DiscogsNamedResource[]? Artists,
        DiscogsLabelResource[]? Labels,
        DiscogsFormatResource[]? Formats,
        DiscogsTrackResponse[]? Tracklist,
        DiscogsIdentifierResponse[]? Identifiers);

    private sealed record DiscogsArtistDetailResponse(
        long Id,
        string? Name,
        string? Profile,
        string? Uri,
        DiscogsNamedResource[]? Aliases,
        DiscogsNamedResource[]? Members,
        string[]? NameVariations);

    private sealed record DiscogsTrackResponse(
        string? Title,
        string? Position,
        string? Duration,
        DiscogsNamedResource[]? Artists);

    private sealed record DiscogsNamedResource(string? Name);

    private sealed record DiscogsLabelResource(string? Name, string? CatalogNumber);

    private sealed record DiscogsFormatResource(string? Name);

    private sealed record DiscogsIdentifierResponse(string? Type, string? Value);
#pragma warning restore CA1812
}
