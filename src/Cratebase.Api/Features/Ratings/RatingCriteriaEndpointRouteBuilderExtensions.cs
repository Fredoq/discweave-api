using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Ratings;

public static class RatingCriteriaEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapRatingCriteriaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/rating-criteria")
            .WithTags("Ratings")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListCriteriaAsync).WithName("ListRatingCriteria");
        _ = group.MapPost("", CreateCriterionAsync).WithName("CreateRatingCriterion");
        _ = group.MapPut("/{criterionId:guid}", UpdateCriterionAsync).WithName("UpdateRatingCriterion");
        _ = group.MapDelete("/{criterionId:guid}", DeleteCriterionAsync).WithName("DeleteRatingCriterion");

        return endpoints;
    }

    private static async Task<IResult> ListCriteriaAsync(
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        RatingCriterion[] criteria = await context.RatingCriteria.AsNoTracking()
            .Where(criterion => criterion.CollectionId == currentCollection.CollectionId)
            .OrderBy(criterion => criterion.SortOrder)
            .ThenBy(criterion => criterion.Name)
            .ToArrayAsync(cancellationToken);
        RatingCriterionResponse[] responses = [.. criteria.Select(RatingEndpointHelpers.ToCriterionResponse)];

        return Results.Ok(new ListResponse<RatingCriterionResponse>(responses, responses.Length, 0, responses.Length));
    }

    private static async Task<IResult> CreateCriterionAsync(
        RatingCriterionRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            RatingTargetType[] targetTypes = RatingEndpointHelpers.ParseTargetTypes(request.TargetTypes);
            var criterion = RatingCriterion.Create(
                RatingCriterionId.New(),
                currentCollection.CollectionId,
                request.Code,
                request.Name,
                targetTypes,
                request.SortOrder ?? 100);
            if (request.IsActive == false)
            {
                criterion.Deactivate();
            }

            bool codeExists = await context.RatingCriteria.AnyAsync(
                existing => existing.CollectionId == currentCollection.CollectionId && existing.Code == criterion.Code,
                cancellationToken);
            if (codeExists)
            {
                return EndpointErrors.Conflict("rating_criterion.code_conflict", "Rating criterion code already exists");
            }

            _ = context.RatingCriteria.Add(criterion);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/rating-criteria/{criterion.Id.Value}", RatingEndpointHelpers.ToCriterionResponse(criterion));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateCriterionAsync(
        Guid criterionId,
        UpdateRatingCriterionRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        RatingCriterion? criterion = await FindCriterionAsync(context, currentCollection.CollectionId, criterionId, cancellationToken);
        if (criterion is null)
        {
            return EndpointErrors.NotFound("rating_criterion.not_found", "Rating criterion was not found");
        }

        try
        {
            RatingTargetType[] targetTypes = RatingEndpointHelpers.ParseTargetTypes(request.TargetTypes);
            criterion.Update(request.Name, targetTypes, request.SortOrder ?? criterion.SortOrder, request.IsActive != false);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(RatingEndpointHelpers.ToCriterionResponse(criterion));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteCriterionAsync(
        Guid criterionId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "rating-criterion", criterionId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        RatingCriterion? criterion = await FindCriterionAsync(context, currentCollection.CollectionId, criterionId, cancellationToken);
        if (criterion is null)
        {
            return EndpointErrors.NotFound("rating_criterion.not_found", "Rating criterion was not found");
        }

        if (criterion.IsProtected)
        {
            return EndpointErrors.BadRequest("rating_criterion.protected", "Protected rating criteria cannot be deleted");
        }

        _ = context.RatingCriteria.Remove(criterion);
        _ = await context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<RatingCriterion?> FindCriterionAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Guid criterionId,
        CancellationToken cancellationToken)
    {
        return await context.RatingCriteria.SingleOrDefaultAsync(
            criterion => criterion.CollectionId == collectionId && criterion.Id == new RatingCriterionId(criterionId),
            cancellationToken);
    }
}
