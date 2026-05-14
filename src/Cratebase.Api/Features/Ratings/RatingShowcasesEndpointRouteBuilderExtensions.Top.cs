using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    private sealed record TopRatingShowcaseRow<TId>(TId Id, string Title, Rating Rating)
        where TId : struct;

    private static async Task<IResult> ListTopArtistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<TopRatingShowcaseRow<ArtistId>> query = (
                from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Artist)
                join artist in context.Artists.AsNoTracking().Where(artist => artist.CollectionId == collectionId)
                    on EF.Property<ArtistId?>(rating, RatingEndpointHelpers.TargetArtistIdProperty) equals (ArtistId?)artist.Id
                select new { artist.Id, Title = artist.Name, rating.Rating })
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new TopRatingShowcaseRow<ArtistId>(row.Id, row.Title, row.Rating));

        return await ListTopAsync(query, criterionId, "artist", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListTopReleasesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<TopRatingShowcaseRow<ReleaseId>> query = (
                from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Release)
                join release in context.Releases.AsNoTracking().Where(release => release.CollectionId == collectionId)
                    on EF.Property<ReleaseId?>(rating, RatingEndpointHelpers.TargetReleaseIdProperty) equals (ReleaseId?)release.Id
                select new { release.Id, release.Summary.Title, rating.Rating })
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new TopRatingShowcaseRow<ReleaseId>(row.Id, row.Title, row.Rating));

        return await ListTopAsync(query, criterionId, "release", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListTopTracksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<TopRatingShowcaseRow<TrackId>> query = (
                from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Track)
                join track in context.Tracks.AsNoTracking().Where(track => track.CollectionId == collectionId)
                    on EF.Property<TrackId?>(rating, RatingEndpointHelpers.TargetTrackIdProperty) equals (TrackId?)track.Id
                select new { track.Id, track.Title, rating.Rating })
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new TopRatingShowcaseRow<TrackId>(row.Id, row.Title, row.Rating));

        return await ListTopAsync(query, criterionId, "track", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListTopLabelsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<TopRatingShowcaseRow<LabelId>> query = (
                from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Label)
                join label in context.Labels.AsNoTracking().Where(label => label.CollectionId == collectionId)
                    on EF.Property<LabelId?>(rating, RatingEndpointHelpers.TargetLabelIdProperty) equals (LabelId?)label.Id
                select new { label.Id, Title = label.Name, rating.Rating })
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Select(row => new TopRatingShowcaseRow<LabelId>(row.Id, row.Title, row.Rating));

        return await ListTopAsync(query, criterionId, "label", id => id.Value, limit, offset, cancellationToken);
    }

    private static async Task<IResult> ListTopAsync<TId>(
        IQueryable<TopRatingShowcaseRow<TId>> query,
        RatingCriterionId criterionId,
        string targetType,
        Func<TId, Guid> targetId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
        where TId : struct
    {
        int total = await query.CountAsync(cancellationToken);
        TopRatingShowcaseRow<TId>[] rows = await query
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
                row.Rating.Value))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }
}
