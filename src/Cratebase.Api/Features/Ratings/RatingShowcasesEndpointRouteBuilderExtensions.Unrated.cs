using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    private sealed record UnratedRatingShowcaseRow<TId>(TId Id, string Title)
        where TId : struct;

    private static async Task<IResult> ListUnratedArtistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<RatingValue> ratings = BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Artist);
        IQueryable<UnratedRatingShowcaseRow<ArtistId>> query = context.Artists.AsNoTracking()
            .Where(artist => artist.CollectionId == collectionId)
            .Where(artist => !ratings.Any(rating =>
                EF.Property<ArtistId?>(rating, RatingEndpointHelpers.TargetArtistIdProperty) == (ArtistId?)artist.Id))
            .Select(artist => new { artist.Id, Title = artist.Name })
            .OrderBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new UnratedRatingShowcaseRow<ArtistId>(row.Id, row.Title));

        return await ListUnratedAsync(query, criterionId, "artist", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListUnratedReleasesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<RatingValue> ratings = BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Release);
        IQueryable<UnratedRatingShowcaseRow<ReleaseId>> query = context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId)
            .Where(release => !ratings.Any(rating =>
                EF.Property<ReleaseId?>(rating, RatingEndpointHelpers.TargetReleaseIdProperty) == (ReleaseId?)release.Id))
            .Select(release => new { release.Id, release.Summary.Title })
            .OrderBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new UnratedRatingShowcaseRow<ReleaseId>(row.Id, row.Title));

        return await ListUnratedAsync(query, criterionId, "release", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListUnratedTracksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<RatingValue> ratings = BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Track);
        IQueryable<UnratedRatingShowcaseRow<TrackId>> query = context.Tracks.AsNoTracking()
            .Where(track => track.CollectionId == collectionId)
            .Where(track => !ratings.Any(rating =>
                EF.Property<TrackId?>(rating, RatingEndpointHelpers.TargetTrackIdProperty) == (TrackId?)track.Id))
            .Select(track => new { track.Id, track.Title })
            .OrderBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new UnratedRatingShowcaseRow<TrackId>(row.Id, row.Title));

        return await ListUnratedAsync(query, criterionId, "track", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListUnratedLabelsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<RatingValue> ratings = BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Label);
        IQueryable<UnratedRatingShowcaseRow<LabelId>> query = context.Labels.AsNoTracking()
            .Where(label => label.CollectionId == collectionId)
            .Where(label => !ratings.Any(rating =>
                EF.Property<LabelId?>(rating, RatingEndpointHelpers.TargetLabelIdProperty) == (LabelId?)label.Id))
            .Select(label => new { label.Id, Title = label.Name })
            .OrderBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new UnratedRatingShowcaseRow<LabelId>(row.Id, row.Title));

        return await ListUnratedAsync(query, criterionId, "label", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListUnratedAsync<TId>(
        IQueryable<UnratedRatingShowcaseRow<TId>> query,
        RatingCriterionId criterionId,
        string targetType,
        Func<TId, Guid> targetId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
        where TId : struct
    {
        int total = await query.CountAsync(cancellationToken);
        UnratedRatingShowcaseRow<TId>[] rows = await query
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        RatingShowcaseItemResponse[] items =
        [
            .. rows.Select(row => new RatingShowcaseItemResponse(
                criterionId.Value,
                targetType,
                targetId(row.Id),
                row.Title,
                null,
                null))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }
}
