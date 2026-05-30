using DiscWeave.Api.Http;
using DiscWeave.Application.Persistence;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.OwnedItems;

public static partial class OwnedItemsEndpointRouteBuilderExtensions
{
    private static async Task<IResult> UpdateDigitalFileAsync(
        Guid ownedItemId,
        UpdateDigitalFileRequest request,
        IUnitOfWork unitOfWork,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        OwnedItem? item = await context.OwnedItems.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new OwnedItemId(ownedItemId),
            cancellationToken);
        if (item is null)
        {
            return EndpointErrors.NotFound("owned_item.not_found", "Owned item was not found");
        }

        if (item.Holding.Medium is not DigitalFile currentDigitalFile)
        {
            return EndpointErrors.BadRequest("owned_item.digital_file_required", "Owned item must reference a digital file");
        }

        try
        {
            var path = FilePath.FromAbsolutePath(request.Path);
            FileImportIdentity identity = string.IsNullOrWhiteSpace(request.ContentHash)
                ? FileImportIdentity.Create(path, request.SizeBytes, request.LastModifiedAt)
                : FileImportIdentity.Create(path, request.SizeBytes, request.LastModifiedAt, request.ContentHash);
            var digitalFile = DigitalFile.Create(
                currentDigitalFile.Code,
                path,
                OwnedItemMapper.ParseAudioFileFormat(request.Format),
                identity);

            item.UpdateHolding(OwnedItemHolding
                .Create(item.Holding.Status, digitalFile)
                .WithDetails(item.Holding.Details));
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
            return EndpointErrors.BadRequest("owned_item.request_invalid", "Owned item digital file request is invalid");
        }
    }
}
