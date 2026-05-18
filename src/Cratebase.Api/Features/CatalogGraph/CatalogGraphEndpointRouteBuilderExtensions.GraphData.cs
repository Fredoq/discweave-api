using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private sealed partial record GraphData(
        Dictionary<ArtistId, Artist> Artists,
        Dictionary<LabelId, Label> Labels,
        Dictionary<ReleaseId, Release> Releases,
        Dictionary<TrackId, Track> Tracks,
        Dictionary<OwnedItemId, OwnedItem> OwnedItems,
        IReadOnlyList<Credit> Credits,
        IReadOnlyList<ArtistRelation> ArtistRelations,
        IReadOnlyList<TrackRelation> TrackRelations)
    {
        private const string CreditContributorArtistId = "_contributorArtistId";

        public static async Task<GraphData?> LoadArtistAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ArtistId artistId,
            CancellationToken cancellationToken)
        {
            Artist? artist = await context.Artists.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.Id == artistId, cancellationToken);
            if (artist is null)
            {
                return null;
            }

            Credit[] credits = await context.Credits.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && EF.Property<ArtistId>(item, CreditContributorArtistId) == artistId)
                .ToArrayAsync(cancellationToken);
            ArtistRelation[] artistRelations = await context.ArtistRelations.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (item.SourceArtistId == artistId || item.TargetArtistId == artistId))
                .ToArrayAsync(cancellationToken);

            ReleaseId[] releaseIds = [.. credits.Select(ReleaseCreditTargetId).Where(id => id.HasValue).Select(id => id!.Value).Distinct()];
            TrackId[] trackIds = [.. credits.Select(TrackCreditTargetId).Where(id => id.HasValue).Select(id => id!.Value).Distinct()];
            ArtistId[] artistIds =
            [
                artistId,
                .. artistRelations.SelectMany(item => new[] { item.SourceArtistId, item.TargetArtistId }).Distinct()
            ];

            Artist[] artists = await LoadArtistsAsync(context, collectionId, artistIds, cancellationToken);
            Release[] releases = await LoadReleasesAsync(context, collectionId, releaseIds, cancellationToken);
            Track[] tracks = await LoadTracksAsync(context, collectionId, trackIds, cancellationToken);

            return Create(new GraphDataContent
            {
                Artists = artists,
                Releases = releases,
                Tracks = tracks,
                Credits = credits,
                ArtistRelations = artistRelations
            });
        }

        public static async Task<GraphData?> LoadReleaseAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            ReleaseId releaseId,
            CancellationToken cancellationToken)
        {
            Release? release = await ReleaseQuery(context)
                .SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.Id == releaseId, cancellationToken);
            if (release is null)
            {
                return null;
            }

            TrackId[] trackIds = [.. release.Tracklist.Select(item => item.TrackId).Distinct()];
            LabelId[] labelIds = [.. ReleaseLabelIds(release).Distinct()];
            Track[] tracks = await LoadTracksAsync(context, collectionId, trackIds, cancellationToken);
            Label[] labels = await LoadLabelsAsync(context, collectionId, labelIds, cancellationToken);
            OwnedItem[] ownedItems = await LoadOwnedItemsForReleasesAsync(context, collectionId, [releaseId], cancellationToken);
            Credit[] credits = await LoadCreditsForReleasesAsync(context, collectionId, [releaseId], cancellationToken);

            return Create(new GraphDataContent
            {
                Labels = labels,
                Releases = [release],
                Tracks = tracks,
                OwnedItems = ownedItems,
                Credits = credits
            });
        }

        public static async Task<GraphData?> LoadTrackAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            TrackId trackId,
            CancellationToken cancellationToken)
        {
            Track? track = await TrackQuery(context)
                .SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.Id == trackId, cancellationToken);
            if (track is null)
            {
                return null;
            }

            OwnedItem[] ownedItems = await LoadOwnedItemsForTracksAsync(context, collectionId, [trackId], cancellationToken);
            Credit[] credits = await LoadCreditsForTracksAsync(context, collectionId, [trackId], cancellationToken);
            TrackRelation[] trackRelations = await context.TrackRelations.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (item.SourceTrackId == trackId || item.TargetTrackId == trackId))
                .ToArrayAsync(cancellationToken);
            Release[] releases = await ReleaseQuery(context)
                .Where(item => item.CollectionId == collectionId && item.Tracklist.Any(tracklistItem => tracklistItem.TrackId == trackId))
                .ToArrayAsync(cancellationToken);
            TrackId[] relatedTrackIds =
            [
                trackId,
                .. trackRelations.SelectMany(item => new[] { item.SourceTrackId, item.TargetTrackId }).Distinct()
            ];
            Track[] tracks = await LoadTracksAsync(context, collectionId, relatedTrackIds, cancellationToken);

            return Create(new GraphDataContent
            {
                Releases = releases,
                Tracks = tracks,
                OwnedItems = ownedItems,
                Credits = credits,
                TrackRelations = trackRelations
            });
        }

        public static async Task<GraphData?> LoadOwnedItemAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            OwnedItemId ownedItemId,
            CancellationToken cancellationToken)
        {
            OwnedItem? ownedItem = await context.OwnedItems.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.Id == ownedItemId, cancellationToken);
            if (ownedItem is null)
            {
                return null;
            }

            Release[] releases = ownedItem.Target is ReleaseOwnedItemTarget releaseTarget
                ? await LoadReleasesAsync(context, collectionId, [releaseTarget.ReleaseId], cancellationToken)
                : [];
            Track[] tracks = ownedItem.Target is TrackOwnedItemTarget trackTarget
                ? await LoadTracksAsync(context, collectionId, [trackTarget.TrackId], cancellationToken)
                : [];

            return Create(new GraphDataContent
            {
                Releases = releases,
                Tracks = tracks,
                OwnedItems = [ownedItem]
            });
        }

        public static async Task<GraphData?> LoadLabelAsync(
            CratebaseDbContext context,
            CollectionId collectionId,
            LabelId labelId,
            CancellationToken cancellationToken)
        {
            Label? label = await context.Labels.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CollectionId == collectionId && item.Id == labelId, cancellationToken);
            if (label is null)
            {
                return null;
            }

            Release[] releases = await ReleaseQuery(context)
                .Where(item => item.CollectionId == collectionId && item.Labels.Any(releaseLabel => releaseLabel.LabelId == labelId))
                .ToArrayAsync(cancellationToken);
            ReleaseId[] releaseIds = [.. releases.Select(item => item.Id)];
            OwnedItem[] ownedItems = await LoadOwnedItemsForReleasesAsync(context, collectionId, releaseIds, cancellationToken);

            return Create(new GraphDataContent
            {
                Labels = [label],
                Releases = releases,
                OwnedItems = ownedItems
            });
        }

        private static GraphData Create(GraphDataContent content)
        {
            return new GraphData(
                content.Artists.ToDictionary(item => item.Id),
                content.Labels.ToDictionary(item => item.Id),
                content.Releases.ToDictionary(item => item.Id),
                content.Tracks.ToDictionary(item => item.Id),
                content.OwnedItems.ToDictionary(item => item.Id),
                content.Credits,
                content.ArtistRelations,
                content.TrackRelations);
        }

        private sealed record GraphDataContent
        {
            public IReadOnlyList<Artist> Artists { get; init; } = [];

            public IReadOnlyList<Label> Labels { get; init; } = [];

            public IReadOnlyList<Release> Releases { get; init; } = [];

            public IReadOnlyList<Track> Tracks { get; init; } = [];

            public IReadOnlyList<OwnedItem> OwnedItems { get; init; } = [];

            public IReadOnlyList<Credit> Credits { get; init; } = [];

            public IReadOnlyList<ArtistRelation> ArtistRelations { get; init; } = [];

            public IReadOnlyList<TrackRelation> TrackRelations { get; init; } = [];
        }

        private static IQueryable<Release> ReleaseQuery(CratebaseDbContext context)
        {
            return context.Releases.AsNoTracking().AsSplitQuery().Include("_genres").Include("_tags");
        }

        private static IQueryable<Track> TrackQuery(CratebaseDbContext context)
        {
            return context.Tracks.AsNoTracking().AsSplitQuery().Include("_genres").Include("_tags");
        }
    }
}
