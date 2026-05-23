using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private sealed partial record GraphData
    {
        private static async Task<Artist[]> LoadArtistsAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ArtistId[] artistIds,
            CancellationToken cancellationToken)
        {
            return artistIds.Length == 0
                ? []
                : await context.Artists.AsNoTracking()
                    .Where(item => item.CollectionId == collectionId && artistIds.Contains(item.Id))
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<Label[]> LoadLabelsAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            LabelId[] labelIds,
            CancellationToken cancellationToken)
        {
            return labelIds.Length == 0
                ? []
                : await context.Labels.AsNoTracking()
                    .Where(item => item.CollectionId == collectionId && labelIds.Contains(item.Id))
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<Release[]> LoadReleasesAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ReleaseId[] releaseIds,
            CancellationToken cancellationToken)
        {
            return releaseIds.Length == 0
                ? []
                : await ReleaseQuery(context)
                    .Where(item => item.CollectionId == collectionId && releaseIds.Contains(item.Id))
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<Track[]> LoadTracksAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            TrackId[] trackIds,
            CancellationToken cancellationToken)
        {
            return trackIds.Length == 0
                ? []
                : await TrackQuery(context)
                    .Where(item => item.CollectionId == collectionId && trackIds.Contains(item.Id))
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<OwnedItem[]> LoadOwnedItemsForReleasesAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ReleaseId[] releaseIds,
            CancellationToken cancellationToken)
        {
            Guid[] releaseIdValues = [.. releaseIds.Select(id => id.Value)];

            return releaseIdValues.Length == 0
                ? []
                : await context.OwnedItems.FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM owned_items
                    WHERE collection_id = {collectionId.Value}
                        AND target_release_id = ANY({releaseIdValues})
                    """)
                    .AsNoTracking()
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<Playlist[]> LoadPlaylistsAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ReleaseId[] releaseIds,
            TrackId[] trackIds,
            CancellationToken cancellationToken)
        {
            Playlist[] playlists = await context.Playlists.AsNoTracking()
                .Include(playlist => playlist.Entries)
                .Where(playlist => playlist.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken);
            HashSet<ReleaseId> releaseSet = [.. releaseIds];
            HashSet<TrackId> trackSet = [.. trackIds];

            return
            [
                .. playlists.Where(playlist => playlist.Entries.Any(entry =>
                    (entry.ReleaseId is PresentOptionalValue<ReleaseId> releaseId && releaseSet.Contains(releaseId.Value)) ||
                    (entry.TrackId is PresentOptionalValue<TrackId> trackId && trackSet.Contains(trackId.Value))))
            ];
        }

        private static async Task<OwnedItem[]> LoadOwnedItemsForTracksAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            TrackId[] trackIds,
            CancellationToken cancellationToken)
        {
            Guid[] trackIdValues = [.. trackIds.Select(id => id.Value)];

            return trackIdValues.Length == 0
                ? []
                : await context.OwnedItems.FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM owned_items
                    WHERE collection_id = {collectionId.Value}
                        AND target_track_id = ANY({trackIdValues})
                    """)
                    .AsNoTracking()
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<Credit[]> LoadCreditsForReleasesAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ReleaseId[] releaseIds,
            CancellationToken cancellationToken)
        {
            Guid[] releaseIdValues = [.. releaseIds.Select(id => id.Value)];

            return releaseIdValues.Length == 0
                ? []
                : await context.Credits.FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM credits
                    WHERE collection_id = {collectionId.Value}
                        AND target_release_id = ANY({releaseIdValues})
                    """)
                    .AsNoTracking()
                    .ToArrayAsync(cancellationToken);
        }

        private static async Task<Credit[]> LoadCreditsForTracksAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            TrackId[] trackIds,
            CancellationToken cancellationToken)
        {
            Guid[] trackIdValues = [.. trackIds.Select(id => id.Value)];

            return trackIdValues.Length == 0
                ? []
                : await context.Credits.FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM credits
                    WHERE collection_id = {collectionId.Value}
                        AND target_track_id = ANY({trackIdValues})
                    """)
                    .AsNoTracking()
                    .ToArrayAsync(cancellationToken);
        }

        private static ReleaseId? ReleaseCreditTargetId(Credit credit)
        {
            return credit.Target is ReleaseCreditTarget target ? target.ReleaseId : null;
        }

        private static TrackId? TrackCreditTargetId(Credit credit)
        {
            return credit.Target is TrackCreditTarget target ? target.TrackId : null;
        }
    }
}
