using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Playlists;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private static CatalogGraphContextResponse PlaylistContext(Playlist playlist, GraphData data)
    {
        Release[] releases =
        [
            .. data.Releases.Values
                .OrderBy(release => release.Summary.Title)
        ];
        Track[] tracks =
        [
            .. data.Tracks.Values
                .OrderBy(track => track.Title)
        ];
        OwnedItem[] ownedItems =
        [
            .. data.OwnedItems.Values
                .Where(item => item.Target switch
                {
                    ReleaseOwnedItemTarget releaseTarget => releases.Any(release => release.Id == releaseTarget.ReleaseId),
                    TrackOwnedItemTarget trackTarget => tracks.Any(track => track.Id == trackTarget.TrackId),
                    _ => false
                })
        ];
        Label[] labels =
        [
            .. releases
                .SelectMany(ReleaseLabelIds)
                .Distinct()
                .Select(id => data.Labels.GetValueOrDefault(id))
                .WhereNotNull()
        ];

        return Response(
            Entity(playlist.Id.Value, PlaylistEntityType, playlist.Name, playlist.Type.ToString(), PlaylistSummary(playlist)),
            new GraphSections
            {
                Releases = [.. releases.Select(release => Link(release.Id.Value, ReleaseEntityType, release.Summary.Title, null, "playlist entry"))],
                Tracks = [.. tracks.Select(track => Link(track.Id.Value, TrackEntityType, track.Title, null, "playlist entry"))],
                OwnedCopies = [.. ownedItems.Select(item => OwnedItemLink(item, data, "playlist coverage"))],
                Labels = [.. labels.Select(label => Link(label.Id.Value, LabelEntityType, label.Name, null, LabelRelation))],
                Media = [.. ownedItems.Select(item => Link(item.Id.Value, OwnedItemEntityType, item.Holding.Medium.Code, StatusCode(item.Holding.Status), MediaRelation))],
                CollectorSignals = Signals(ownedItems)
            });
    }
}
