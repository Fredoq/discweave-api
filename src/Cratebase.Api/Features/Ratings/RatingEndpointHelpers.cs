using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

internal static class RatingEndpointHelpers
{
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
        CratebaseDbContext context,
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

    public static async Task<RatingTargetDisplay[]> LoadTargetDisplaysAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingTargetType targetType,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            RatingTargetType.Artist => [.. (await context.Artists.AsNoTracking()
                .Where(artist => artist.CollectionId == collectionId)
                .OrderBy(artist => artist.Name)
                .ToArrayAsync(cancellationToken))
                .Select(artist => new RatingTargetDisplay(artist.Id.Value, "artist", artist.Name, null))],
            RatingTargetType.Release => [.. (await context.Releases.AsNoTracking()
                .Where(release => release.CollectionId == collectionId)
                .OrderBy(release => release.Summary.Title)
                .ToArrayAsync(cancellationToken))
                .Select(release => new RatingTargetDisplay(release.Id.Value, "release", release.Summary.Title, null))],
            RatingTargetType.Track => [.. (await context.Tracks.AsNoTracking()
                .Where(track => track.CollectionId == collectionId)
                .OrderBy(track => track.Title)
                .ToArrayAsync(cancellationToken))
                .Select(track => new RatingTargetDisplay(track.Id.Value, "track", track.Title, null))],
            RatingTargetType.Label => [.. (await context.Labels.AsNoTracking()
                .Where(label => label.CollectionId == collectionId)
                .OrderBy(label => label.Name)
                .ToArrayAsync(cancellationToken))
                .Select(label => new RatingTargetDisplay(label.Id.Value, "label", label.Name, null))],
            _ => []
        };
    }
}

internal sealed record RatingTargetDisplay(Guid TargetId, string TargetType, string Title, string? Subtitle);
