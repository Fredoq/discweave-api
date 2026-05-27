using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private static IReadOnlyList<CatalogGraphContextResponse.LinkResponse> PlaylistLinksForRelease(
        ReleaseId releaseId,
        GraphData data)
    {
        return PlaylistLinksForReleases([releaseId], data);
    }

    private static IReadOnlyList<CatalogGraphContextResponse.LinkResponse> PlaylistLinksForReleases(
        IEnumerable<ReleaseId> releaseIds,
        GraphData data)
    {
        HashSet<ReleaseId> ids = [.. releaseIds];
        return
        [
            .. data.Playlists.Values
                .Where(playlist => playlist.Entries.Any(entry => entry.ReleaseId is PresentOptionalValue<ReleaseId> id && ids.Contains(id.Value)))
                .OrderBy(playlist => playlist.Name)
                .Select(playlist => Link(playlist.Id.Value, PlaylistEntityType, playlist.Name, playlist.Type.ToString(), "playlist backlink"))
        ];
    }

    private static IReadOnlyList<CatalogGraphContextResponse.LinkResponse> PlaylistLinksForTrack(
        TrackId trackId,
        GraphData data)
    {
        return
        [
            .. data.Playlists.Values
                .Where(playlist => playlist.Entries.Any(entry => entry.TrackId is PresentOptionalValue<TrackId> id && id.Value == trackId))
                .OrderBy(playlist => playlist.Name)
                .Select(playlist => Link(playlist.Id.Value, PlaylistEntityType, playlist.Name, playlist.Type.ToString(), "playlist backlink"))
        ];
    }

    private static ReleaseId[] PlaylistReleaseIds(Playlist playlist)
    {
        return
        [
            .. playlist.Entries
                .Select(entry => entry.ReleaseId)
                .OfType<PresentOptionalValue<ReleaseId>>()
                .Select(id => id.Value)
                .Distinct()
        ];
    }

    private static TrackId[] PlaylistTrackIds(Playlist playlist)
    {
        return
        [
            .. playlist.Entries
                .Select(entry => entry.TrackId)
                .OfType<PresentOptionalValue<TrackId>>()
                .Select(id => id.Value)
                .Distinct()
        ];
    }

    private static string PlaylistSummary(Playlist playlist)
    {
        return playlist.Description is PresentOptionalValue<string> description
            ? description.Value
            : playlist.Type switch
            {
                PlaylistType.Manual => $"{playlist.Entries.Count} manual entries",
                PlaylistType.Smart => "Smart playlist rules",
                _ => "Playlist"
            };
    }
}
