using Cratebase.Api.Auth;
using Cratebase.Api.Features.Settings;
using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Application.Security;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.OwnedItems;

public static partial class OwnedItemsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOwnedItemsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/owned-items")
            .WithTags("Owned Items")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateOwnedItemAsync).WithName("CreateOwnedItem");
        _ = group.MapGet("/{ownedItemId:guid}", GetOwnedItemAsync).WithName("GetOwnedItem");
        _ = group.MapGet("", ListOwnedItemsAsync).WithName("ListOwnedItems");
        _ = group.MapPut("/{ownedItemId:guid}", UpdateOwnedItemAsync).WithName("UpdateOwnedItem");
        _ = group.MapDelete("/{ownedItemId:guid}", DeleteOwnedItemAsync).WithName("DeleteOwnedItem");

        return endpoints;
    }

    private static async Task<IResult> CreateOwnedItemAsync(
        CreateOwnedItemRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request.Medium);
            CollectionDictionaryEntry mediaEntry = await DictionaryValidation.RequireActiveEntryAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.MediaType,
                request.Medium.Type ?? string.Empty,
                "medium.type_invalid",
                "Medium type is invalid",
                cancellationToken);
            IMedium medium = OwnedItemMapper.CreateMedium(request.Medium, mediaEntry);
            var item = OwnedItem.Create(
                currentCollection.CollectionId,
                OwnedItemId.New(),
                OwnedItemMapper.CreateTarget(request.TargetType, request.TargetId),
                OwnedItemMapper.ParseOwnershipStatus(request.Status),
                medium);
            item.UpdateHolding(OwnedItemMapper.CreateHolding(item.Holding.Medium, request.Status, request.Condition, request.StorageLocation));
            IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
            items.Add(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            OwnedItemResponse response = await OwnedItemResponseMapper.ToResponseAsync(
                context,
                currentCollection.CollectionId,
                item,
                cancellationToken);

            return Results.Created($"/api/owned-items/{item.Id}", response);
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return EndpointErrors.BadRequest("owned_item.request_invalid", "Owned item request is invalid");
        }
        catch (ReferencedResourceMissingException)
        {
            return EndpointErrors.Conflict("owned_item.target_conflict", "Owned item target does not exist");
        }
    }

    private static async Task<IResult> GetOwnedItemAsync(
        Guid ownedItemId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        OwnedItem? item = await context.OwnedItems.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new OwnedItemId(ownedItemId),
            cancellationToken);

        return item is null
            ? EndpointErrors.NotFound("owned_item.not_found", "Owned item was not found")
            : Results.Ok(await OwnedItemResponseMapper.ToResponseAsync(
                context,
                currentCollection.CollectionId,
                item,
                cancellationToken));
    }

    private static async Task<IResult> UpdateOwnedItemAsync(
        Guid ownedItemId,
        UpdateOwnedItemRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
        OwnedItem? item = await items.TryFindAsync(new OwnedItemId(ownedItemId), cancellationToken);
        if (item is null || item.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound("owned_item.not_found", "Owned item was not found");
        }

        try
        {
            if (!TryCreateUpdatedTarget(request, out OwnedItemTarget? target, out IResult targetError))
            {
                return targetError;
            }

            if (target is not null)
            {
                item.UpdateTarget(target);
            }

            IMedium medium = item.Holding.Medium;
            if (request.Medium is not null)
            {
                CollectionDictionaryEntry mediaEntry = await DictionaryValidation.RequireActiveEntryAsync(
                    context,
                    currentCollection.CollectionId,
                    DictionaryKind.MediaType,
                    request.Medium.Type ?? string.Empty,
                    "medium.type_invalid",
                    "Medium type is invalid",
                    cancellationToken);
                medium = OwnedItemMapper.CreateMedium(request.Medium, mediaEntry);
            }

            item.UpdateHolding(OwnedItemMapper.CreateHolding(medium, request.Status, request.Condition, request.StorageLocation));
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            OwnedItemResponse response = await OwnedItemResponseMapper.ToResponseAsync(
                context,
                currentCollection.CollectionId,
                item,
                cancellationToken);

            return Results.Ok(response);
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return EndpointErrors.BadRequest("owned_item.request_invalid", "Owned item request is invalid");
        }
        catch (ReferencedResourceMissingException)
        {
            return EndpointErrors.Conflict("owned_item.target_conflict", "Owned item target does not exist");
        }
    }

    private static bool TryCreateUpdatedTarget(
        UpdateOwnedItemRequest request,
        out OwnedItemTarget? target,
        out IResult error)
    {
        target = null;
        error = null!;

        if (request.TargetType is null && request.TargetId is null)
        {
            return true;
        }

        if (request.TargetType is null || request.TargetId is null)
        {
            error = EndpointErrors.BadRequest("owned_item.target_shape_invalid", "Owned item target requires both targetType and targetId");
            return false;
        }

        target = OwnedItemMapper.CreateTarget(request.TargetType, request.TargetId.Value);
        return true;
    }

    private static async Task<IResult> DeleteOwnedItemAsync(
        Guid ownedItemId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "owned-item", ownedItemId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
        OwnedItem? item = await items.TryFindAsync(new OwnedItemId(ownedItemId), cancellationToken);
        if (item is null || item.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound("owned_item.not_found", "Owned item was not found");
        }

        try
        {
            items.Delete(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("owned_item.delete_conflict", "Owned item has dependent data");
        }
    }
}
