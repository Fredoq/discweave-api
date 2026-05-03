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

namespace Cratebase.Api.Features.ArtistRelations;

public static class ArtistRelationsEndpointRouteBuilderExtensions
{
    private const string ArtistRelationNotFoundCode = "artist_relation.not_found";
    private const string ArtistRelationNotFoundMessage = "Artist relation was not found";

    public static IEndpointRouteBuilder MapArtistRelationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/artist-relations")
            .WithTags("Artist Relations")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateArtistRelationAsync).WithName("CreateArtistRelation");
        _ = group.MapGet("/{relationId:guid}", GetArtistRelationAsync).WithName("GetArtistRelation");
        _ = group.MapGet("", ListArtistRelationsAsync).WithName("ListArtistRelations");
        _ = group.MapPut("/{relationId:guid}", UpdateArtistRelationAsync).WithName("UpdateArtistRelation");
        _ = group.MapDelete("/{relationId:guid}", DeleteArtistRelationAsync).WithName("DeleteArtistRelation");

        return endpoints;
    }

    private static async Task<IResult> CreateArtistRelationAsync(
        ArtistRelationRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await ArtistsExistAsync(request.SourceArtistId, request.TargetArtistId, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("artist_relation.artist_conflict", "Artist relation references a missing artist");
            }

            ArtistRelation relation = CreateRelation(request, currentCollection.CollectionId, ArtistRelationId.New());
            unitOfWork.GetRepository<ArtistRelation, ArtistRelationId>().Add(relation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/artist-relations/{relation.Id.Value}", ArtistRelationMapper.ToResponse(relation));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetArtistRelationAsync(
        Guid relationId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ArtistRelation? relation = await context.ArtistRelations.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ArtistRelationId(relationId),
            cancellationToken);

        return relation is null
            ? EndpointErrors.NotFound(ArtistRelationNotFoundCode, ArtistRelationNotFoundMessage)
            : Results.Ok(ArtistRelationMapper.ToResponse(relation));
    }

    private static async Task<IResult> ListArtistRelationsAsync(
        [AsParameters] ArtistRelationListRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<ArtistRelation> relations = ApplyFilters(
            context.ArtistRelations.AsNoTracking().Where(relation => relation.CollectionId == currentCollection.CollectionId),
            request.SourceArtistId,
            request.TargetArtistId,
            request.Type);
        int total = await relations.CountAsync(cancellationToken);
        ArtistRelation[] page = await relations.OrderBy(relation => relation.Id).Skip(normalizedOffset).Take(normalizedLimit).ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<ArtistRelationResponse>([.. page.Select(ArtistRelationMapper.ToResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateArtistRelationAsync(
        Guid relationId,
        ArtistRelationRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        IRepository<ArtistRelation, ArtistRelationId> relations = unitOfWork.GetRepository<ArtistRelation, ArtistRelationId>();
        ArtistRelation? relation = await relations.TryFindAsync(new ArtistRelationId(relationId), cancellationToken);
        if (relation is null)
        {
            return EndpointErrors.NotFound(ArtistRelationNotFoundCode, ArtistRelationNotFoundMessage);
        }

        try
        {
            if (relation.CollectionId != currentCollection.CollectionId)
            {
                return EndpointErrors.NotFound(ArtistRelationNotFoundCode, ArtistRelationNotFoundMessage);
            }

            if (!await ArtistsExistAsync(request.SourceArtistId, request.TargetArtistId, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("artist_relation.artist_conflict", "Artist relation references a missing artist");
            }

            ArtistRelationPeriod? period = ArtistRelationMapper.CreatePeriod(request.StartYear, request.EndYear);
            if (period is null)
            {
                relation.Update(new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), ArtistRelationMapper.ParseType(request.Type));
            }
            else
            {
                relation.Update(new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), ArtistRelationMapper.ParseType(request.Type), period);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ArtistRelationMapper.ToResponse(relation));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteArtistRelationAsync(
        Guid relationId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "artist-relation", relationId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<ArtistRelation, ArtistRelationId> relations = unitOfWork.GetRepository<ArtistRelation, ArtistRelationId>();
        ArtistRelation? relation = await relations.TryFindAsync(new ArtistRelationId(relationId), cancellationToken);
        if (relation is null || relation.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound(ArtistRelationNotFoundCode, ArtistRelationNotFoundMessage);
        }

        try
        {
            relations.Delete(relation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("artist_relation.delete_conflict", "Artist relation has dependent data");
        }
    }

    private static ArtistRelation CreateRelation(ArtistRelationRequest request, CollectionId collectionId, ArtistRelationId relationId)
    {
        ArtistRelationPeriod? period = ArtistRelationMapper.CreatePeriod(request.StartYear, request.EndYear);

        return period is null
            ? ArtistRelation.Create(relationId, collectionId, new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), ArtistRelationMapper.ParseType(request.Type))
            : ArtistRelation.Create(relationId, collectionId, new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), ArtistRelationMapper.ParseType(request.Type), period);
    }

    private static IQueryable<ArtistRelation> ApplyFilters(IQueryable<ArtistRelation> relations, Guid? sourceArtistId, Guid? targetArtistId, string? type)
    {
        if (sourceArtistId is { } sourceId)
        {
            relations = relations.Where(relation => relation.SourceArtistId == new ArtistId(sourceId));
        }

        if (targetArtistId is { } targetId)
        {
            relations = relations.Where(relation => relation.TargetArtistId == new ArtistId(targetId));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            ArtistRelationType parsedType = ArtistRelationMapper.ParseType(type);
            relations = relations.Where(relation => relation.Type == parsedType);
        }

        return relations;
    }

    private static async Task<bool> ArtistsExistAsync(
        Guid sourceArtistId,
        Guid targetArtistId,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        ArtistId sourceId = new(sourceArtistId);
        ArtistId targetId = new(targetArtistId);

        return await context.Artists.CountAsync(artist => artist.CollectionId == collectionId && (artist.Id == sourceId || artist.Id == targetId), cancellationToken) == 2;
    }
}
