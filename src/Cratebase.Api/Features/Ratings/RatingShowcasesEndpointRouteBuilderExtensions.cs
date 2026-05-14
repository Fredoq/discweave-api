using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    private static async Task<IResult> ListShowcaseAsync(
        Guid criterionId,
        string targetType,
        string? mode,
        string? scope,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        _ = scope;

        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        try
        {
            RatingTargetType parsedTargetType = RatingTargetTypeCodes.FromCode(targetType);
            RatingCriterion? criterion = await FindUsableCriterionAsync(
                context,
                currentCollection.CollectionId,
                new RatingCriterionId(criterionId),
                parsedTargetType,
                cancellationToken);
            if (criterion is null)
            {
                return EndpointErrors.NotFound("rating_criterion.not_found", "Rating criterion was not found");
            }

            string normalizedMode = string.IsNullOrWhiteSpace(mode) ? "top" : mode.Trim();

            return normalizedMode switch
            {
                "unrated" => await ListUnratedShowcaseAsync(
                    context,
                    currentCollection.CollectionId,
                    criterion.Id,
                    parsedTargetType,
                    normalizedLimit,
                    normalizedOffset,
                    cancellationToken),
                "top" => await ListTopShowcaseAsync(
                    context,
                    currentCollection.CollectionId,
                    criterion.Id,
                    parsedTargetType,
                    normalizedLimit,
                    normalizedOffset,
                    cancellationToken),
                _ => EndpointErrors.BadRequest("rating_showcase.mode_invalid", "Rating showcase mode is invalid")
            };
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> ListTopShowcaseAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        RatingValue[] ratings = await LoadRatingsAsync(context, collectionId, criterionId, targetType, cancellationToken);
        Dictionary<Guid, RatingTargetDisplay> displays = (await RatingEndpointHelpers.LoadTargetDisplaysAsync(
                context,
                collectionId,
                targetType,
                cancellationToken))
            .ToDictionary(display => display.TargetId);
        RatingValue[] rankedRatings = [.. ratings
            .Where(rating => displays.ContainsKey(RatingEndpointHelpers.TargetId(rating.Target)))
            .OrderByDescending(rating => rating.Rating.Value)
            .ThenBy(rating => displays[RatingEndpointHelpers.TargetId(rating.Target)].Title)
            .ThenBy(rating => RatingEndpointHelpers.TargetId(rating.Target))];

        RatingShowcaseItemResponse[] items = [.. rankedRatings
            .Skip(offset)
            .Take(limit)
            .Select(rating =>
            {
                RatingTargetDisplay display = displays[RatingEndpointHelpers.TargetId(rating.Target)];
                return new RatingShowcaseItemResponse(
                    criterionId.Value,
                    display.TargetType,
                    display.TargetId,
                    display.Title,
                    display.Subtitle,
                    rating.Rating.Value);
            })];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, rankedRatings.Length));
    }

    private static async Task<IResult> ListUnratedShowcaseAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        RatingValue[] ratings = await LoadRatingsAsync(context, collectionId, criterionId, targetType, cancellationToken);
        HashSet<Guid> ratedTargetIds = [.. ratings.Select(rating => RatingEndpointHelpers.TargetId(rating.Target))];
        RatingTargetDisplay[] unratedDisplays = [.. (await RatingEndpointHelpers.LoadTargetDisplaysAsync(
                context,
                collectionId,
                targetType,
                cancellationToken))
            .Where(display => !ratedTargetIds.Contains(display.TargetId))
            .OrderBy(display => display.Title)
            .ThenBy(display => display.TargetId)];

        RatingShowcaseItemResponse[] items = [.. unratedDisplays
            .Skip(offset)
            .Take(limit)
            .Select(display => new RatingShowcaseItemResponse(
                criterionId.Value,
                display.TargetType,
                display.TargetId,
                display.Title,
                display.Subtitle,
                null))];

        return Results.Ok(new RatingShowcaseResponse(items, limit, offset, unratedDisplays.Length));
    }

    private static async Task<RatingValue[]> LoadRatingsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        CancellationToken cancellationToken)
    {
        return await RatingEndpointHelpers.FilterByTargetType(
                context.RatingValues.AsNoTracking().Where(value => value.CollectionId == collectionId && value.CriterionId == criterionId),
                targetType)
            .ToArrayAsync(cancellationToken);
    }
}
