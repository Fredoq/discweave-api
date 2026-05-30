using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Ratings;

internal static class RatingEndpointHelpers
{
    public const string TargetArtistIdProperty = "_targetArtistId";
    public const string TargetLabelIdProperty = "_targetLabelId";
    public const string TargetReleaseIdProperty = "_targetReleaseId";
    public const string TargetTrackIdProperty = "_targetTrackId";

    public static RatingCriterionResponse ToCriterionResponse(RatingCriterion criterion)
    {
        return new RatingCriterionResponse(
            criterion.Id.Value,
            criterion.Code,
            criterion.Name,
            [.. criterion.TargetTypes.Select(RatingTargetTypeCodes.ToCode)],
            criterion.SortOrder,
            criterion.IsActive,
            criterion.IsBuiltin,
            criterion.IsProtected);
    }

    public static RatingValueResponse ToValueResponse(RatingValue value)
    {
        RatingTarget target = value.Target;

        return new RatingValueResponse(
            value.Id.Value,
            value.CriterionId.Value,
            RatingTargetTypeCodes.ToCode(target.Type),
            TargetId(target),
            value.Rating.Value);
    }

    public static RatingTargetType[] ParseTargetTypes(IReadOnlyList<string>? targetTypes)
    {
        return targetTypes is null
            ? throw new DomainException("rating_criterion.target_required", "Rating criterion must target at least one entity type")
            : [.. targetTypes.Select(RatingTargetTypeCodes.FromCode)];
    }

    public static RatingTarget CreateTarget(RatingTargetType targetType, Guid targetId)
    {
        return targetType switch
        {
            RatingTargetType.Artist => RatingTarget.ForArtist(new ArtistId(targetId)),
            RatingTargetType.Release => RatingTarget.ForRelease(new ReleaseId(targetId)),
            RatingTargetType.Track => RatingTarget.ForTrack(new TrackId(targetId)),
            RatingTargetType.Label => RatingTarget.ForLabel(new LabelId(targetId)),
            _ => throw new DomainException("rating_target.type_invalid", "Rating target type is invalid")
        };
    }

    public static Guid TargetId(RatingTarget target)
    {
        return target switch
        {
            ArtistRatingTarget artistTarget => artistTarget.ArtistId.Value,
            ReleaseRatingTarget releaseTarget => releaseTarget.ReleaseId.Value,
            TrackRatingTarget trackTarget => trackTarget.TrackId.Value,
            LabelRatingTarget labelTarget => labelTarget.LabelId.Value,
            _ => throw new InvalidOperationException("Rating target type is not supported")
        };
    }

    public static async Task<bool> TargetExistsAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        RatingTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            RatingTargetType.Artist => await context.Artists.AnyAsync(
                artist => artist.CollectionId == collectionId && artist.Id == new ArtistId(targetId),
                cancellationToken),
            RatingTargetType.Release => await context.Releases.AnyAsync(
                release => release.CollectionId == collectionId && release.Id == new ReleaseId(targetId),
                cancellationToken),
            RatingTargetType.Track => await context.Tracks.AnyAsync(
                track => track.CollectionId == collectionId && track.Id == new TrackId(targetId),
                cancellationToken),
            RatingTargetType.Label => await context.Labels.AnyAsync(
                label => label.CollectionId == collectionId && label.Id == new LabelId(targetId),
                cancellationToken),
            _ => false
        };
    }

    public static IQueryable<RatingValue> FilterByTargetType(IQueryable<RatingValue> query, RatingTargetType targetType)
    {
        string targetTypeCode = RatingTargetTypeCodes.ToCode(targetType);

        return query.Where(value => EF.Property<string>(value, "_targetType") == targetTypeCode);
    }

    public static IQueryable<RatingValue> FilterByTargetId(IQueryable<RatingValue> query, RatingTargetType targetType, Guid targetId)
    {
        return targetType switch
        {
            RatingTargetType.Artist => query.Where(value =>
                EF.Property<ArtistId?>(value, TargetArtistIdProperty) == new ArtistId(targetId)),
            RatingTargetType.Release => query.Where(value =>
                EF.Property<ReleaseId?>(value, TargetReleaseIdProperty) == new ReleaseId(targetId)),
            RatingTargetType.Track => query.Where(value =>
                EF.Property<TrackId?>(value, TargetTrackIdProperty) == new TrackId(targetId)),
            RatingTargetType.Label => query.Where(value =>
                EF.Property<LabelId?>(value, TargetLabelIdProperty) == new LabelId(targetId)),
            _ => query.Where(_ => false)
        };
    }
}
