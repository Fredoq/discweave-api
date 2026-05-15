using Cratebase.Api.Http;
using Cratebase.Application.Catalog.Releases;
using Cratebase.Application.Errors;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<IResult> DeleteReleaseAsync(
        Guid releaseId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        IReleaseCoverStorage coverStorage,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "release", releaseId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        Release? release = await context.Releases.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ReleaseId(releaseId),
            cancellationToken);
        if (release is null)
        {
            return EndpointErrors.NotFound("release.not_found", "Release was not found");
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            CoverImage? coverImage = TryGetCoverImage(release);
            TrackId[] linkedTrackIds = [.. release.Tracklist.Select(track => track.TrackId).Distinct()];
            Credit[] collectionCredits = await context.Credits
                .Where(credit => credit.CollectionId == currentCollection.CollectionId)
                .ToArrayAsync(cancellationToken);
            Credit[] releaseCredits =
                [.. collectionCredits.Where(credit => credit.Target is ReleaseCreditTarget target && target.ReleaseId == release.Id)];
            OwnedItem[] releaseOwnedItems = await context.OwnedItems
                .Where(item =>
                    item.CollectionId == currentCollection.CollectionId &&
                    EF.Property<ReleaseId?>(item, "_targetReleaseId") == release.Id)
                .ToArrayAsync(cancellationToken);

            TrackId[] removableTrackIds = linkedTrackIds.Length == 0
                ? []
                : await FindUnusedReleaseTrackIdsAsync(release, linkedTrackIds, context, currentCollection.CollectionId, cancellationToken);
            Credit[] removableTrackCredits =
                [.. collectionCredits.Where(credit => credit.Target is TrackCreditTarget target && removableTrackIds.Contains(target.TrackId))];
            Track[] removableTracks = removableTrackIds.Length == 0
                ? []
                : await context.Tracks
                    .Where(track => track.CollectionId == currentCollection.CollectionId && removableTrackIds.Contains(track.Id))
                    .ToArrayAsync(cancellationToken);

            context.Credits.RemoveRange(releaseCredits);
            context.Credits.RemoveRange(removableTrackCredits);
            context.OwnedItems.RemoveRange(releaseOwnedItems);
            release.ReplaceTracklist([]);
            _ = context.Releases.Remove(release);
            context.Tracks.RemoveRange(removableTracks);

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (coverImage is not null)
            {
                await coverStorage.DeleteAsync(coverImage.StorageKey, cancellationToken);
            }

            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("release.delete_conflict", "Release has dependent data");
        }
    }

    private static async Task<TrackId[]> FindUnusedReleaseTrackIdsAsync(
        Release release,
        IReadOnlyCollection<TrackId> linkedTrackIds,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Release[] collectionReleases = await context.Releases
            .Where(candidate => candidate.CollectionId == collectionId)
            .ToArrayAsync(cancellationToken);
        var trackIdsLinkedToOtherReleases = collectionReleases
            .Where(candidate => candidate.Id != release.Id)
            .SelectMany(candidate => candidate.Tracklist)
            .Select(releaseTrack => releaseTrack.TrackId)
            .ToHashSet();
        TrackRelation[] relations = await context.TrackRelations
            .Where(relation =>
                relation.CollectionId == collectionId &&
                (linkedTrackIds.Contains(relation.SourceTrackId) || linkedTrackIds.Contains(relation.TargetTrackId)))
            .ToArrayAsync(cancellationToken);
        var trackIdsWithRelations = relations
            .SelectMany(relation => new[] { relation.SourceTrackId, relation.TargetTrackId })
            .ToHashSet();
        OwnedItem[] collectionOwnedItems = await context.OwnedItems
            .Where(item => item.CollectionId == collectionId)
            .ToArrayAsync(cancellationToken);
        var trackIdsWithOwnedItems = collectionOwnedItems
            .Select(item => item.Target)
            .OfType<TrackOwnedItemTarget>()
            .Select(target => target.TrackId)
            .ToHashSet();

        return
        [
            .. linkedTrackIds.Where(trackId =>
                !trackIdsLinkedToOtherReleases.Contains(trackId) &&
                !trackIdsWithRelations.Contains(trackId) &&
                !trackIdsWithOwnedItems.Contains(trackId))
        ];
    }
}
