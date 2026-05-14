using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    private static async Task<IResult> ListUnratedArtistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        IQueryable<RatingValue> ratings = BaseRatingQuery(context, collectionId, criterionId, RatingTargetType.Artist);
        var query = context.Artists.AsNoTracking()
            .Where(artist => artist.CollectionId == collectionId)
            .Where(artist => !ratings.Any(rating =>
                EF.Property<ArtistId?>(rating, RatingEndpointHelpers.TargetArtistIdProperty) == (ArtistId?)artist.Id))
            .Select(artist => new { artist.Id, Title = artist.Name });

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(row => row.Title)
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
                null))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
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
        var query = context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId)
            .Where(release => !ratings.Any(rating =>
                EF.Property<ReleaseId?>(rating, RatingEndpointHelpers.TargetReleaseIdProperty) == (ReleaseId?)release.Id))
            .Select(release => new { release.Id, release.Summary.Title });

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(row => row.Title)
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
                null))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
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
        var query = context.Tracks.AsNoTracking()
            .Where(track => track.CollectionId == collectionId)
            .Where(track => !ratings.Any(rating =>
                EF.Property<TrackId?>(rating, RatingEndpointHelpers.TargetTrackIdProperty) == (TrackId?)track.Id))
            .Select(track => new { track.Id, track.Title });

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(row => row.Title)
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
                null))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
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
        var query = context.Labels.AsNoTracking()
            .Where(label => label.CollectionId == collectionId)
            .Where(label => !ratings.Any(rating =>
                EF.Property<LabelId?>(rating, RatingEndpointHelpers.TargetLabelIdProperty) == (LabelId?)label.Id))
            .Select(label => new { label.Id, Title = label.Name });

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(row => row.Title)
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
                null))
        ];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, total));
    }
}
