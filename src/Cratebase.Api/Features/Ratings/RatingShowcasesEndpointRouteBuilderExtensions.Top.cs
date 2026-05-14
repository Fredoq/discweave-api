using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    private static async Task<IResult> ListTopArtistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var query =
            from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Artist)
            join artist in context.Artists.AsNoTracking().Where(artist => artist.CollectionId == collectionId)
                on EF.Property<ArtistId?>(rating, RatingEndpointHelpers.TargetArtistIdProperty) equals (ArtistId?)artist.Id
            select new { artist.Id, Title = artist.Name, rating.Rating };

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        RatingShowcaseItemResponse[] items =
        [
            .. rows.Select(row => new RatingShowcaseItemResponse(
                criterionId.Value,
                "artist",
                row.Id.Value,
                row.Title,
                null,
                row.Rating.Value))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }

    private static async Task<IResult> ListTopReleasesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var query =
            from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Release)
            join release in context.Releases.AsNoTracking().Where(release => release.CollectionId == collectionId)
                on EF.Property<ReleaseId?>(rating, RatingEndpointHelpers.TargetReleaseIdProperty) equals (ReleaseId?)release.Id
            select new { release.Id, release.Summary.Title, rating.Rating };

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        RatingShowcaseItemResponse[] items =
        [
            .. rows.Select(row => new RatingShowcaseItemResponse(
                criterionId.Value,
                "release",
                row.Id.Value,
                row.Title,
                null,
                row.Rating.Value))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }

    private static async Task<IResult> ListTopTracksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var query =
            from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Track)
            join track in context.Tracks.AsNoTracking().Where(track => track.CollectionId == collectionId)
                on EF.Property<TrackId?>(rating, RatingEndpointHelpers.TargetTrackIdProperty) equals (TrackId?)track.Id
            select new { track.Id, track.Title, rating.Rating };

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        RatingShowcaseItemResponse[] items =
        [
            .. rows.Select(row => new RatingShowcaseItemResponse(
                criterionId.Value,
                "track",
                row.Id.Value,
                row.Title,
                null,
                row.Rating.Value))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }

    private static async Task<IResult> ListTopLabelsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var query =
            from rating in BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Label)
            join label in context.Labels.AsNoTracking().Where(label => label.CollectionId == collectionId)
                on EF.Property<LabelId?>(rating, RatingEndpointHelpers.TargetLabelIdProperty) equals (LabelId?)label.Id
            select new { label.Id, Title = label.Name, rating.Rating };

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.Title)
            .ThenBy(row => row.Id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        RatingShowcaseItemResponse[] items =
        [
            .. rows.Select(row => new RatingShowcaseItemResponse(
                criterionId.Value,
                "label",
                row.Id.Value,
                row.Title,
                null,
                row.Rating.Value))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }
}
