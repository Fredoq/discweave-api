using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.Errors;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Ratings;

public static partial class RatingsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapRatingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder ratings = endpoints.MapGroup("/api/ratings")
            .WithTags("Ratings")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);

        _ = ratings.MapGet("", ListRatingsAsync).WithName("ListRatings");
        _ = ratings.MapPut("/{targetType}/{targetId:guid}/{criterionId:guid}", UpsertRatingAsync).WithName("UpsertRating");
        _ = ratings.MapDelete("/{targetType}/{targetId:guid}/{criterionId:guid}", DeleteRatingAsync).WithName("DeleteRating");

        RouteGroupBuilder showcases = endpoints.MapGroup("/api/rating-showcases")
            .WithTags("Ratings")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = showcases.MapGet("", ListShowcaseAsync).WithName("ListRatingShowcase");

        return endpoints;
    }

    private static async Task<IResult> ListRatingsAsync(
        [AsParameters] RatingListRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<RatingValue> query = context.RatingValues.AsNoTracking()
            .Where(value => value.CollectionId == currentCollection.CollectionId);
        try
        {
            if (!string.IsNullOrWhiteSpace(request.TargetType))
            {
                RatingTargetType parsedTargetType = RatingTargetTypeCodes.FromCode(request.TargetType);
                query = RatingEndpointHelpers.FilterByTargetType(query, parsedTargetType);
            }
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }

        if (request.CriterionId is { } parsedCriterionId)
        {
            query = query.Where(value => value.CriterionId == new RatingCriterionId(parsedCriterionId));
        }

        RatingValue[] values = await query.ToArrayAsync(cancellationToken);
        if (request.TargetId is { } parsedTargetId)
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
        [AsParameters] RatingTargetRouteRequest route,
        RatingValueRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            RatingTargetType parsedTargetType = RatingTargetTypeCodes.FromCode(route.TargetType);
            RatingCriterion? criterion = await FindUsableCriterionAsync(
                context,
                currentCollection.CollectionId,
                new RatingCriterionId(route.CriterionId),
                parsedTargetType,
                cancellationToken);
            if (criterion is null)
            {
                return EndpointErrors.NotFound("rating_criterion.not_found", "Rating criterion was not found");
            }

            if (!await RatingEndpointHelpers.TargetExistsAsync(context, currentCollection.CollectionId, parsedTargetType, route.TargetId, cancellationToken))
            {
                return EndpointErrors.NotFound("rating_target.not_found", "Rating target was not found");
            }

            var rating = Rating.FromValue(request.Value);
            RatingValue? value = await FindRatingValueAsync(
                context,
                currentCollection.CollectionId,
                parsedTargetType,
                route.TargetId,
                criterion.Id,
                cancellationToken);
            if (value is null)
            {
                value = RatingValue.Create(
                    currentCollection.CollectionId,
                    RatingValueId.New(),
                    criterion.Id,
                    RatingEndpointHelpers.CreateTarget(parsedTargetType, route.TargetId),
                    rating);
                _ = context.RatingValues.Add(value);
            }
            else
            {
                value.UpdateRating(rating);
            }

            try
            {
                _ = await context.SaveChangesAsync(cancellationToken);
            }
            catch (ResourceConflictException exception) when (exception.Conflict == ResourceConflictException.RatingValueTarget)
            {
                context.Entry(value).State = EntityState.Detached;
                RatingValue? existing = await FindRatingValueAsync(
                    context,
                    currentCollection.CollectionId,
                    parsedTargetType,
                    route.TargetId,
                    criterion.Id,
                    cancellationToken);
                if (existing is null)
                {
                    throw;
                }

                existing.UpdateRating(rating);
                _ = await context.SaveChangesAsync(cancellationToken);
                value = existing;
            }

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
        HttpRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            string expectedConfirmation = $"rating:{targetType}:{targetId}:{criterionId}";
            if (!DeleteConfirmation.Matches(request, expectedConfirmation))
            {
                return EndpointErrors.DeleteConfirmationRequired();
            }

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
        DiscWeaveDbContext context,
        CollectionId collectionId,
        RatingCriterionId criterionId,
        RatingTargetType targetType,
        CancellationToken cancellationToken)
    {
        RatingCriterion? criterion = await context.RatingCriteria.SingleOrDefaultAsync(
            entity => entity.CollectionId == collectionId && entity.Id == criterionId,
            cancellationToken);
        if (criterion is null)
        {
            return null;
        }

        EnsureCriterionCanRateTarget(criterion, targetType);

        return criterion;
    }

    private static void EnsureCriterionCanRateTarget(RatingCriterion criterion, RatingTargetType targetType)
    {
        if (!criterion.IsActive)
        {
            throw new DomainException("rating_criterion.inactive", "Rating criterion is inactive");
        }

        if (!criterion.AppliesTo(targetType))
        {
            throw new DomainException("rating_criterion.target_invalid", "Rating criterion does not apply to the target type");
        }
    }

    private static async Task<RatingValue?> FindRatingValueAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        RatingTargetType targetType,
        Guid targetId,
        RatingCriterionId criterionId,
        CancellationToken cancellationToken)
    {
        IQueryable<RatingValue> query = RatingEndpointHelpers.FilterByTargetType(
            context.RatingValues.Where(value => value.CollectionId == collectionId && value.CriterionId == criterionId),
            targetType);
        query = RatingEndpointHelpers.FilterByTargetId(query, targetType, targetId);

        return await query.SingleOrDefaultAsync(cancellationToken);
    }
}
