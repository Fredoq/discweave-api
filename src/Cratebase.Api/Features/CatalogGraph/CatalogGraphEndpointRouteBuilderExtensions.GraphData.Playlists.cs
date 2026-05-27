using Cratebase.Api.Features.Playlists;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private sealed partial record GraphData
    {
        public static async Task<GraphData?> LoadPlaylistAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            PlaylistId playlistId,
            CancellationToken cancellationToken)
        {
            Playlist? playlist = await context.Playlists.AsNoTracking()
                .Include(item => item.Entries)
                .SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.Id == playlistId, cancellationToken);
            if (playlist is null)
            {
                return null;
            }

            PlaylistItemResponse[] results = await PlaylistMapper.ResolveResultsAsync(playlist, context, cancellationToken);
            ReleaseId[] playlistReleaseIds =
            [
                .. results
                    .Where(result => result.Kind == PlaylistEntry.ReleaseKind)
                    .Select(result => new ReleaseId(result.Id))
                    .Distinct()
            ];
            TrackId[] playlistTrackIds =
            [
                .. results
                    .Where(result => result.Kind == PlaylistEntry.TrackKind)
                    .Select(result => new TrackId(result.Id))
                    .Distinct()
            ];
            Release[] entryReleases = await LoadReleasesAsync(context, collectionId, playlistReleaseIds, cancellationToken);
            Track[] tracks = await LoadTracksAsync(context, collectionId, playlistTrackIds, cancellationToken);
            Release[] trackReleases = playlistTrackIds.Length == 0
                ? []
                : await ReleaseQuery(context)
                    .Where(item => item.CollectionId == collectionId && item.Tracklist.Any(tracklistItem => playlistTrackIds.Contains(tracklistItem.TrackId)))
                    .ToArrayAsync(cancellationToken);
            Release[] releases = [.. entryReleases.Concat(trackReleases).DistinctBy(item => item.Id)];
            ReleaseId[] releaseIds = [.. releases.Select(item => item.Id)];
            LabelId[] labelIds = [.. releases.SelectMany(ReleaseLabelIds).Distinct()];
            Label[] labels = await LoadLabelsAsync(context, collectionId, labelIds, cancellationToken);
            OwnedItem[] releaseOwnedItems = await LoadOwnedItemsForReleasesAsync(context, collectionId, releaseIds, cancellationToken);
            OwnedItem[] trackOwnedItems = await LoadOwnedItemsForTracksAsync(context, collectionId, playlistTrackIds, cancellationToken);

            return Create(new GraphDataContent
            {
                Labels = labels,
                Releases = releases,
                Tracks = tracks,
                OwnedItems = [.. releaseOwnedItems.Concat(trackOwnedItems).DistinctBy(item => item.Id)],
                Playlists = [playlist]
            });
        }
    }
}
