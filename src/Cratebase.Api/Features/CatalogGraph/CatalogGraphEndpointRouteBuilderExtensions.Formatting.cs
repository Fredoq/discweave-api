using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private static CatalogGraphContextResponse.LinkResponse OwnedItemLink(OwnedItem item, GraphData data, string relation)
    {
        string title = item.Target switch
        {
            ReleaseOwnedItemTarget target when data.Releases.TryGetValue(target.ReleaseId, out Release? release) => release.Summary.Title,
            TrackOwnedItemTarget target when data.Tracks.TryGetValue(target.TrackId, out Track? track) => track.Title,
            _ => "Owned item"
        };

        return Link(item.Id.Value, OwnedItemEntityType, title, $"{StatusCode(item.Holding.Status)} on {item.Holding.Medium.Code}", relation);
    }

    private static CatalogGraphContextResponse.LinkResponse? CreditTargetLink(Credit credit, GraphData data)
    {
        return credit.Target switch
        {
            ReleaseCreditTarget target when data.Releases.TryGetValue(target.ReleaseId, out Release? release) => Link(release.Id.Value, ReleaseEntityType, release.Summary.Title, credit.Role, CreditRelation),
            TrackCreditTarget target when data.Tracks.TryGetValue(target.TrackId, out Track? track) => Link(track.Id.Value, TrackEntityType, track.Title, credit.Role, CreditRelation),
            _ => null
        };
    }

    private static IEnumerable<LabelId> ReleaseLabelIds(Release release)
    {
        foreach (ReleaseLabel label in release.Labels)
        {
            yield return label.LabelId;
        }

        if (release.Summary.Metadata.LabelId.HasValue)
        {
            yield return release.Summary.Metadata.LabelId.Match(value => value, () => default);
        }
    }

    private static string ArtistRelationTitle(ArtistRelation relation, GraphData data)
    {
        return $"{data.Artists.GetValueOrDefault(relation.SourceArtistId)?.Name ?? "Artist"} to {data.Artists.GetValueOrDefault(relation.TargetArtistId)?.Name ?? "Artist"}";
    }

    private static string TrackRelationTitle(TrackRelation relation, GraphData data)
    {
        return $"{data.Tracks.GetValueOrDefault(relation.SourceTrackId)?.Title ?? "Track"} to {data.Tracks.GetValueOrDefault(relation.TargetTrackId)?.Title ?? "Track"}";
    }

    private static CatalogGraphContextResponse.LinkResponse? RelatedArtistLink(ArtistId originArtistId, ArtistRelation relation, GraphData data)
    {
        ArtistId relatedArtistId = relation.SourceArtistId == originArtistId
            ? relation.TargetArtistId
            : relation.SourceArtistId;

        return data.Artists.TryGetValue(relatedArtistId, out Artist? artist)
            ? Link(artist.Id.Value, ArtistEntityType, artist.Name, relation.Type, "artist relation")
            : null;
    }

    private static CatalogGraphContextResponse.LinkResponse? RelatedTrackLink(TrackId originTrackId, TrackRelation relation, GraphData data)
    {
        TrackId relatedTrackId = relation.SourceTrackId == originTrackId
            ? relation.TargetTrackId
            : relation.SourceTrackId;

        return data.Tracks.TryGetValue(relatedTrackId, out Track? track)
            ? Link(track.Id.Value, TrackEntityType, track.Title, relation.RelationType, "track relation")
            : null;
    }

    private static IReadOnlyList<string> Signals(IReadOnlyList<OwnedItem> items)
    {
        return [.. items.SelectMany(item => new[] { item.Holding.Medium.Code, StatusCode(item.Holding.Status) }).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static string StatusCode(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Owned => "owned",
            OwnershipStatus.Wanted => "wanted",
            OwnershipStatus.Sold => "sold",
            OwnershipStatus.NeedsDigitization => "needsDigitization",
            _ => throw new InvalidOperationException("Ownership status is not supported")
        };
    }

    private sealed record GraphSections
    {
        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Artists { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Releases { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Tracks { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> OwnedCopies { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Labels { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Playlists { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Credits { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Relations { get; init; } = [];

        public IReadOnlyList<CatalogGraphContextResponse.LinkResponse> Media { get; init; } = [];

        public IReadOnlyList<string> CollectorSignals { get; init; } = [];
    }
}
