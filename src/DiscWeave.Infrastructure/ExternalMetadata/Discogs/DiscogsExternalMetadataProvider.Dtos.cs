using System.Text.Json.Serialization;

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
        string? Released,
        string? Uri,
        DiscogsNamedResource[]? Artists,
        string[]? Genres,
        string[]? Styles,
        DiscogsLabelResource[]? Labels,
        DiscogsFormatResource[]? Formats,
        DiscogsTrackResponse[]? Tracklist,
        DiscogsIdentifierResponse[]? Identifiers,
        [property: JsonPropertyName("extraartists")]
        DiscogsNamedResource[]? ExtraArtists);

    private sealed record DiscogsArtistDetailResponse(
        long Id,
        string? Name,
        string? Profile,
        string? Uri,
        DiscogsNamedResource[]? Aliases,
        DiscogsNamedResource[]? Members,
        string[]? NameVariations);

    private sealed record DiscogsTrackResponse(
        [property: JsonPropertyName("type_")]
        string? Type,
        string? Title,
        string? Position,
        string? Duration,
        DiscogsNamedResource[]? Artists,
        [property: JsonPropertyName("extraartists")]
        DiscogsNamedResource[]? ExtraArtists);

    private sealed record DiscogsNamedResource(string? Name, string? Role);

    private sealed record DiscogsLabelResource(string? Name, [property: JsonPropertyName("catno")] string? CatalogNumber);

    private sealed record DiscogsFormatResource(string? Name, string[]? Descriptions);

    private sealed record DiscogsIdentifierResponse(string? Type, string? Value);
#pragma warning restore CA1812
}
