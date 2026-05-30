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

public static class RatingCriteriaEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapRatingCriteriaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/rating-criteria")
            .WithTags("Ratings")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListCriteriaAsync).WithName("ListRatingCriteria");
        _ = group.MapPost("", CreateCriterionAsync).WithName("CreateRatingCriterion");
        _ = group.MapPut("/{criterionId:guid}", ReplaceCriterionAsync).WithName("ReplaceRatingCriterion");
        _ = group.MapPatch("/{criterionId:guid}", UpdateCriterionAsync).WithName("UpdateRatingCriterion");
        _ = group.MapDelete("/{criterionId:guid}", DeleteCriterionAsync).WithName("DeleteRatingCriterion");

        return endpoints;
    }

    private static async Task<IResult> ListCriteriaAsync(
        DiscWeaveDbContext context,
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
        DiscWeaveDbContext context,
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
        catch (ResourceConflictException exception) when (exception.Conflict == ResourceConflictException.RatingCriterionCode)
        {
            return EndpointErrors.Conflict("rating_criterion.code_conflict", "Rating criterion code already exists");
        }
    }

    private static async Task<IResult> ReplaceCriterionAsync(
        Guid criterionId,
        ReplaceRatingCriterionRequest request,
        DiscWeaveDbContext context,
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
            criterion.Update(request.Name, targetTypes, request.SortOrder, request.IsActive);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(RatingEndpointHelpers.ToCriterionResponse(criterion));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateCriterionAsync(
        Guid criterionId,
        UpdateRatingCriterionRequest request,
        DiscWeaveDbContext context,
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
            RatingTargetType[] targetTypes = request.TargetTypes is null
                ? [.. criterion.TargetTypes]
                : RatingEndpointHelpers.ParseTargetTypes(request.TargetTypes);
            criterion.Update(
                request.Name ?? criterion.Name,
                targetTypes,
                request.SortOrder ?? criterion.SortOrder,
                request.IsActive ?? criterion.IsActive);
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
        DiscWeaveDbContext context,
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
        DiscWeaveDbContext context,
        CollectionId collectionId,
        Guid criterionId,
        CancellationToken cancellationToken)
    {
        return await context.RatingCriteria.SingleOrDefaultAsync(
            criterion => criterion.CollectionId == collectionId && criterion.Id == new RatingCriterionId(criterionId),
            cancellationToken);
    }
}
