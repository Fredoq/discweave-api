using DiscWeave.Api.Auth;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Api.Http;
using DiscWeave.Application.Errors;
using DiscWeave.Application.Persistence;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Relations;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.ArtistRelations;

public static partial class ArtistRelationsEndpointRouteBuilderExtensions
{
    private const string ArtistRelationNotFoundCode = "artist_relation.not_found";
    private const string ArtistRelationNotFoundMessage = "Artist relation was not found";

    public static IEndpointRouteBuilder MapArtistRelationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/artist-relations")
            .WithTags("Artist Relations")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
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
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await ArtistsExistAsync(request.SourceArtistId, request.TargetArtistId, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("artist_relation.artist_conflict", "Artist relation references a missing artist");
            }

            string relationType = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.ArtistRelationType,
                ArtistRelationMapper.ParseType(request.Type),
                "artist_relation.type_invalid",
                "Artist relation type is invalid",
                cancellationToken);
            ArtistRelation relation = CreateRelation(request, currentCollection.CollectionId, ArtistRelationId.New(), relationType);
            unitOfWork.GetRepository<ArtistRelation, ArtistRelationId>().Add(relation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/artist-relations/{relation.Id.Value}",
                await ToResponseAsync(relation, context, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetArtistRelationAsync(
        Guid relationId,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ArtistRelation? relation = await context.ArtistRelations.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ArtistRelationId(relationId),
            cancellationToken);

        return relation is null
            ? EndpointErrors.NotFound(ArtistRelationNotFoundCode, ArtistRelationNotFoundMessage)
            : Results.Ok(await ToResponseAsync(relation, context, cancellationToken));
    }

    private static async Task<IResult> ListArtistRelationsAsync(
        [AsParameters] ArtistRelationListRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        try
        {
            string? relationType = string.IsNullOrWhiteSpace(request.Type)
                ? null
                : await DictionaryValidation.RequireCodeAsync(
                    context,
                    currentCollection.CollectionId,
                    DictionaryKind.ArtistRelationType,
                    ArtistRelationMapper.ParseType(request.Type),
                    "artist_relation.type_invalid",
                    "Artist relation type is invalid",
                    cancellationToken);
            IQueryable<ArtistRelation> relations = ApplyFilters(
                context.ArtistRelations.AsNoTracking().Where(relation => relation.CollectionId == currentCollection.CollectionId),
                request.SourceArtistId,
                request.TargetArtistId,
                relationType);
            int total = await relations.CountAsync(cancellationToken);
            ArtistRelation[] page = await relations.OrderBy(relation => relation.Id).Skip(normalizedOffset).Take(normalizedLimit).ToArrayAsync(cancellationToken);

            return Results.Ok(new ListResponse<ArtistRelationResponse>(
                await ToResponsesAsync(page, context, cancellationToken),
                normalizedLimit,
                normalizedOffset,
                total));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateArtistRelationAsync(
        Guid relationId,
        ArtistRelationRequest request,
        IUnitOfWork unitOfWork,
        DiscWeaveDbContext context,
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

            string relationType = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.ArtistRelationType,
                ArtistRelationMapper.ParseType(request.Type),
                "artist_relation.type_invalid",
                "Artist relation type is invalid",
                cancellationToken);
            ArtistRelationPeriod? period = ArtistRelationMapper.CreatePeriod(request.StartYear, request.EndYear);
            if (period is null)
            {
                relation.Update(new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), relationType);
            }
            else
            {
                relation.Update(new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), relationType, period);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ToResponseAsync(relation, context, cancellationToken));
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

    private static ArtistRelation CreateRelation(ArtistRelationRequest request, CollectionId collectionId, ArtistRelationId relationId, string relationType)
    {
        ArtistRelationPeriod? period = ArtistRelationMapper.CreatePeriod(request.StartYear, request.EndYear);

        return period is null
            ? ArtistRelation.Create(relationId, collectionId, new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), relationType)
            : ArtistRelation.Create(relationId, collectionId, new ArtistId(request.SourceArtistId), new ArtistId(request.TargetArtistId), relationType, period);
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
            relations = relations.Where(relation => relation.Type == type);
        }

        return relations;
    }

    private static async Task<bool> ArtistsExistAsync(
        Guid sourceArtistId,
        Guid targetArtistId,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        ArtistId sourceId = new(sourceArtistId);
        ArtistId targetId = new(targetArtistId);

        return sourceArtistId == targetArtistId
            ? await context.Artists.AnyAsync(artist => artist.CollectionId == collectionId && artist.Id == sourceId, cancellationToken)
            : await context.Artists.CountAsync(artist => artist.CollectionId == collectionId && (artist.Id == sourceId || artist.Id == targetId), cancellationToken) == 2;
    }
}
