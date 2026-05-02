using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.OwnedItems;

public static class OwnedItemsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOwnedItemsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/owned-items").WithTags("Owned Items");
        _ = group.MapPost("/", CreateOwnedItemAsync).WithName("CreateOwnedItem");
        _ = group.MapGet("/{ownedItemId:guid}", GetOwnedItemAsync).WithName("GetOwnedItem");
        _ = group.MapGet("/", ListOwnedItemsAsync).WithName("ListOwnedItems");
        _ = group.MapPut("/{ownedItemId:guid}", UpdateOwnedItemAsync).WithName("UpdateOwnedItem");
        _ = group.MapDelete("/{ownedItemId:guid}", DeleteOwnedItemAsync).WithName("DeleteOwnedItem");

        return endpoints;
    }

    private static async Task<IResult> CreateOwnedItemAsync(
        CreateOwnedItemRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request.Medium);
            IMedium medium = OwnedItemMapper.CreateMedium(request.Medium);
            var item = OwnedItem.Create(
                OwnedItemId.New(),
                OwnedItemMapper.CreateTarget(request.TargetType, request.TargetId),
                OwnedItemMapper.ParseOwnershipStatus(request.Status),
                medium);
            item.UpdateHolding(OwnedItemMapper.CreateHolding(item.Holding.Medium, request.Status, request.Condition, request.StorageLocation));
            IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
            items.Add(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/owned-items/{item.Id}", OwnedItemMapper.ToResponse(item));
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

    private static async Task<IResult> GetOwnedItemAsync(Guid ownedItemId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        OwnedItem? item = await context.OwnedItems.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new OwnedItemId(ownedItemId), cancellationToken);

        return item is null
            ? EndpointErrors.NotFound("owned_item.not_found", "Owned item was not found")
            : Results.Ok(OwnedItemMapper.ToResponse(item));
    }

    private static async Task<IResult> ListOwnedItemsAsync(
        string? status,
        string? medium,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<OwnedItem> items = context.OwnedItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!OwnedItemMapper.TryParseOwnershipStatus(status, out OwnershipStatus normalizedStatus))
            {
                return EndpointErrors.BadRequest("owned_item.status_invalid", "Owned item status is invalid");
            }

            items = items.Where(item => EF.Property<OwnershipStatus>(item, "_status") == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(medium))
        {
            string normalizedMedium = medium.Trim();
            items = items.Where(item => EF.Property<string>(item, "_mediumType") == normalizedMedium);
        }

        int total = await items.CountAsync(cancellationToken);
        OwnedItem[] page = await items
            .OrderBy(item => item.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<OwnedItemResponse>([.. page.Select(OwnedItemMapper.ToResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateOwnedItemAsync(
        Guid ownedItemId,
        UpdateOwnedItemRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
        OwnedItem? item = await items.TryFindAsync(new OwnedItemId(ownedItemId), cancellationToken);
        if (item is null)
        {
            return EndpointErrors.NotFound("owned_item.not_found", "Owned item was not found");
        }

        try
        {
            item.UpdateHolding(OwnedItemMapper.CreateHolding(item.Holding.Medium, request.Status, request.Condition, request.StorageLocation));
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(OwnedItemMapper.ToResponse(item));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return EndpointErrors.BadRequest("owned_item.request_invalid", "Owned item request is invalid");
        }
    }

    private static async Task<IResult> DeleteOwnedItemAsync(
        Guid ownedItemId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "owned-item", ownedItemId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
        OwnedItem? item = await items.TryFindAsync(new OwnedItemId(ownedItemId), cancellationToken);
        if (item is null)
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
