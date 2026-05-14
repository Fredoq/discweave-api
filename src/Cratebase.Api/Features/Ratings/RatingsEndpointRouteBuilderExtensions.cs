using Cratebase.Api.Auth;
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
    public static IEndpointRouteBuilder MapRatingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder ratings = endpoints.MapGroup("/api/ratings")
            .WithTags("Ratings")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = ratings.MapGet("", ListRatingsAsync).WithName("ListRatings");
        _ = ratings.MapPut("/{targetType}/{targetId:guid}/{criterionId:guid}", UpsertRatingAsync).WithName("UpsertRating");
        _ = ratings.MapDelete("/{targetType}/{targetId:guid}/{criterionId:guid}", DeleteRatingAsync).WithName("DeleteRating");

        RouteGroupBuilder showcases = endpoints.MapGroup("/api/rating-showcases")
            .WithTags("Ratings")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = showcases.MapGet("", ListShowcaseAsync).WithName("ListRatingShowcase");

        return endpoints;
    }

    private static async Task<IResult> ListRatingsAsync(
        string? targetType,
        Guid? targetId,
        Guid? criterionId,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<RatingValue> query = context.RatingValues.AsNoTracking()
            .Where(value => value.CollectionId == currentCollection.CollectionId);
        try
        {
            if (!string.IsNullOrWhiteSpace(targetType))
            {
                RatingTargetType parsedTargetType = RatingTargetTypeCodes.FromCode(targetType);
                query = RatingEndpointHelpers.FilterByTargetType(query, parsedTargetType);
            }
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }

        if (criterionId is { } parsedCriterionId)
        {
            query = query.Where(value => value.CriterionId == new RatingCriterionId(parsedCriterionId));
        }

        RatingValue[] values = await query.ToArrayAsync(cancellationToken);
        if (targetId is { } parsedTargetId)
        {
            values = [.. values.Where(value => RatingEndpointHelpers.TargetId(value.Target) == parsedTargetId)];
        }

        int total = values.Length;
        RatingValueResponse[] items = [.. values
            .OrderBy(value => RatingEndpointHelpers.TargetId(value.Target))
            .ThenBy(value => value.CriterionId.Value)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .Select(RatingEndpointHelpers.ToValueResponse)];

        return Results.Ok(new ListResponse<RatingValueResponse>(items, normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpsertRatingAsync(
        string targetType,
        Guid targetId,
        Guid criterionId,
        RatingValueRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
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

            if (!await RatingEndpointHelpers.TargetExistsAsync(context, currentCollection.CollectionId, parsedTargetType, targetId, cancellationToken))
            {
                return EndpointErrors.NotFound("rating_target.not_found", "Rating target was not found");
            }

            var rating = Rating.FromValue(request.Value);
            RatingValue? value = await FindRatingValueAsync(
                context,
                currentCollection.CollectionId,
                parsedTargetType,
                targetId,
                criterion.Id,
                cancellationToken);
            if (value is null)
            {
                value = RatingValue.Create(
                    currentCollection.CollectionId,
                    RatingValueId.New(),
                    criterion.Id,
                    RatingEndpointHelpers.CreateTarget(parsedTargetType, targetId),
                    rating);
                _ = context.RatingValues.Add(value);
            }
            else
            {
                value.UpdateRating(rating);
            }

            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(RatingEndpointHelpers.ToValueResponse(value));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteRatingAsync(
        string targetType,
        Guid targetId,
        Guid criterionId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            RatingTargetType parsedTargetType = RatingTargetTypeCodes.FromCode(targetType);
            RatingValue? value = await FindRatingValueAsync(
                context,
                currentCollection.CollectionId,
                parsedTargetType,
                targetId,
                new RatingCriterionId(criterionId),
                cancellationToken);
            if (value is not null)
            {
                _ = context.RatingValues.Remove(value);
                _ = await context.SaveChangesAsync(cancellationToken);
            }

            return Results.NoContent();
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<RatingCriterion?> FindUsableCriterionAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        CancellationToken cancellationToken)
    {
        RatingCriterion? criterion = await context.RatingCriteria.SingleOrDefaultAsync(
            entity => entity.CollectionId == collectionId && entity.Id == criterionId,
            cancellationToken);
        return criterion is null
            ? null
            : !criterion.IsActive
            ? throw new DomainException("rating_criterion.inactive", "Rating criterion is inactive")
            : criterion.AppliesTo(targetType)
                ? criterion
                : throw new DomainException("rating_criterion.target_invalid", "Rating criterion does not apply to the target type");
    }

    private static async Task<RatingValue?> FindRatingValueAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        RatingTargetType targetType,
        Guid targetId,
        RatingCriterionId criterionId,
        CancellationToken cancellationToken)
    {
        RatingValue[] values = await RatingEndpointHelpers.FilterByTargetType(
                context.RatingValues.Where(value => value.CollectionId == collectionId && value.CriterionId == criterionId),
                targetType)
            .ToArrayAsync(cancellationToken);

        return values.SingleOrDefault(value => RatingEndpointHelpers.TargetId(value.Target) == targetId);
    }
}
