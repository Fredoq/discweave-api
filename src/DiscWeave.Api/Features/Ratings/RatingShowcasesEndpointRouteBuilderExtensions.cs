using DiscWeave.Api.Http;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    private const string CollectionShowcaseScope = "collection";

    private static async Task<IResult> ListShowcaseAsync(
        [AsParameters] RatingShowcaseListRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedShowcaseScope(request.Scope))
        {
            return EndpointErrors.BadRequest("rating_showcase.scope_invalid", "Rating showcase scope is invalid");
        }

        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        try
        {
            RatingTargetType parsedTargetType = RatingTargetTypeCodes.FromCode(request.TargetType);
            RatingCriterion? criterion = await FindUsableCriterionAsync(
                context,
                currentCollection.CollectionId,
                new RatingCriterionId(request.CriterionId),
                parsedTargetType,
                cancellationToken);
            if (criterion is null)
            {
                return EndpointErrors.NotFound("rating_criterion.not_found", "Rating criterion was not found");
            }

            string normalizedMode = string.IsNullOrWhiteSpace(request.Mode) ? "top" : request.Mode.Trim().ToLowerInvariant();

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

    private static bool IsSupportedShowcaseScope(string? scope)
    {
        return string.IsNullOrWhiteSpace(scope) || string.Equals(scope.Trim(), CollectionShowcaseScope, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IResult> ListTopShowcaseAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            RatingTargetType.Artist => await ListTopArtistsAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            RatingTargetType.Release => await ListTopReleasesAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            RatingTargetType.Track => await ListTopTracksAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            RatingTargetType.Label => await ListTopLabelsAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            _ => EndpointErrors.BadRequest("rating_target.type_invalid", "Rating target type is invalid")
        };
    }

    private static async Task<IResult> ListUnratedShowcaseAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            RatingTargetType.Artist => await ListUnratedArtistsAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            RatingTargetType.Release => await ListUnratedReleasesAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            RatingTargetType.Track => await ListUnratedTracksAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            RatingTargetType.Label => await ListUnratedLabelsAsync(context, collectionId, criterionId, limit, offset, cancellationToken),
            _ => EndpointErrors.BadRequest("rating_target.type_invalid", "Rating target type is invalid")
        };
    }

    private static IQueryable<RatingValue> BaseRatingQuery(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType)
    {
        return RatingEndpointHelpers.FilterByTargetType(
            context.RatingValues.AsNoTracking().Where(value => value.CollectionId == collectionId && value.CriterionId == criterionId),
            targetType);
    }
}
