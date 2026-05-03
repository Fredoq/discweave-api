using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Application.Security;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.TrackRelations;

public static class TrackRelationsEndpointRouteBuilderExtensions
{
    private const string TrackRelationNotFoundCode = "track_relation.not_found";
    private const string TrackRelationNotFoundMessage = "Track relation was not found";

    public static IEndpointRouteBuilder MapTrackRelationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/track-relations")
            .WithTags("Track Relations")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateTrackRelationAsync).WithName("CreateTrackRelation");
        _ = group.MapGet("/{relationId:guid}", GetTrackRelationAsync).WithName("GetTrackRelation");
        _ = group.MapGet("", ListTrackRelationsAsync).WithName("ListTrackRelations");
        _ = group.MapPut("/{relationId:guid}", UpdateTrackRelationAsync).WithName("UpdateTrackRelation");
        _ = group.MapDelete("/{relationId:guid}", DeleteTrackRelationAsync).WithName("DeleteTrackRelation");

        return endpoints;
    }

    private static async Task<IResult> CreateTrackRelationAsync(
        TrackRelationRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await TracksExistAsync(request.SourceTrackId, request.TargetTrackId, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("track_relation.track_conflict", "Track relation references a missing track");
            }

            var relation = TrackRelation.Create(TrackRelationId.New(), currentCollection.CollectionId, new TrackId(request.SourceTrackId), new TrackId(request.TargetTrackId), TrackRelationMapper.ParseType(request.Type));
            unitOfWork.GetRepository<TrackRelation, TrackRelationId>().Add(relation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/track-relations/{relation.Id.Value}", TrackRelationMapper.ToResponse(relation));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetTrackRelationAsync(
        Guid relationId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        TrackRelation? relation = await context.TrackRelations.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new TrackRelationId(relationId),
            cancellationToken);

        return relation is null
            ? EndpointErrors.NotFound(TrackRelationNotFoundCode, TrackRelationNotFoundMessage)
            : Results.Ok(TrackRelationMapper.ToResponse(relation));
    }

    private static async Task<IResult> ListTrackRelationsAsync(
        [AsParameters] TrackRelationListRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<TrackRelation> relations = ApplyFilters(
            context.TrackRelations.AsNoTracking().Where(relation => relation.CollectionId == currentCollection.CollectionId),
            request.SourceTrackId,
            request.TargetTrackId,
            request.Type);
        int total = await relations.CountAsync(cancellationToken);
        TrackRelation[] page = await relations.OrderBy(relation => relation.Id).Skip(normalizedOffset).Take(normalizedLimit).ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<TrackRelationResponse>([.. page.Select(TrackRelationMapper.ToResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateTrackRelationAsync(
        Guid relationId,
        TrackRelationRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        IRepository<TrackRelation, TrackRelationId> relations = unitOfWork.GetRepository<TrackRelation, TrackRelationId>();
        TrackRelation? relation = await relations.TryFindAsync(new TrackRelationId(relationId), cancellationToken);
        if (relation is null)
        {
            return EndpointErrors.NotFound(TrackRelationNotFoundCode, TrackRelationNotFoundMessage);
        }

        try
        {
            if (relation.CollectionId != currentCollection.CollectionId)
            {
                return EndpointErrors.NotFound(TrackRelationNotFoundCode, TrackRelationNotFoundMessage);
            }

            if (!await TracksExistAsync(request.SourceTrackId, request.TargetTrackId, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("track_relation.track_conflict", "Track relation references a missing track");
            }

            relation.Update(new TrackId(request.SourceTrackId), new TrackId(request.TargetTrackId), TrackRelationMapper.ParseType(request.Type));
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(TrackRelationMapper.ToResponse(relation));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteTrackRelationAsync(
        Guid relationId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "track-relation", relationId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<TrackRelation, TrackRelationId> relations = unitOfWork.GetRepository<TrackRelation, TrackRelationId>();
        TrackRelation? relation = await relations.TryFindAsync(new TrackRelationId(relationId), cancellationToken);
        if (relation is null || relation.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound(TrackRelationNotFoundCode, TrackRelationNotFoundMessage);
        }

        try
        {
            relations.Delete(relation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("track_relation.delete_conflict", "Track relation has dependent data");
        }
    }

    private static IQueryable<TrackRelation> ApplyFilters(IQueryable<TrackRelation> relations, Guid? sourceTrackId, Guid? targetTrackId, string? type)
    {
        if (sourceTrackId is { } sourceId)
        {
            relations = relations.Where(relation => relation.SourceTrackId == new TrackId(sourceId));
        }

        if (targetTrackId is { } targetId)
        {
            relations = relations.Where(relation => relation.TargetTrackId == new TrackId(targetId));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            TrackRelationType parsedType = TrackRelationMapper.ParseType(type);
            relations = relations.Where(relation => relation.RelationType == parsedType);
        }

        return relations;
    }

    private static async Task<bool> TracksExistAsync(
        Guid sourceTrackId,
        Guid targetTrackId,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        TrackId sourceId = new(sourceTrackId);
        TrackId targetId = new(targetTrackId);

        return await context.Tracks.CountAsync(track => track.CollectionId == collectionId && (track.Id == sourceId || track.Id == targetId), cancellationToken) == 2;
    }
}
