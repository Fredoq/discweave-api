using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.OwnedItems;

public static partial class OwnedItemsEndpointRouteBuilderExtensions
{
    private const string TargetTypeProperty = "_targetType";
    private const string TargetReleaseIdProperty = "_targetReleaseId";
    private const string TargetTrackIdProperty = "_targetTrackId";
    private const string MediumTypeProperty = "_mediumType";
    private const string DigitalFileFormatProperty = "_digitalFileFormat";
    private const string StatusProperty = "_status";
    private const string ConditionProperty = "_condition";
    private const string StorageLocationProperty = "_storageLocation";
    private const string ReleaseTargetType = "release";
    private const string TrackTargetType = "track";
    private static readonly AudioFileFormat?[] LossyFormats = [AudioFileFormat.Mp3, AudioFileFormat.Ogg, AudioFileFormat.M4a];
    private static readonly AudioFileFormat?[] LosslessFormats = [AudioFileFormat.Flac, AudioFileFormat.Wav, AudioFileFormat.Aiff, AudioFileFormat.Alac];

    private static async Task<IResult> ListOwnedItemsAsync(
        [AsParameters] OwnedItemListRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<OwnedItem> collectionItems = context.OwnedItems.AsNoTracking()
            .Where(item => item.CollectionId == currentCollection.CollectionId);
        IQueryable<OwnedItem> items = collectionItems;

        if (!ApplyStatusFilter(ref items, request.Status, out IResult statusError))
        {
            return statusError;
        }

        if (!ApplyConditionFilter(ref items, request.Condition, out IResult conditionError))
        {
            return conditionError;
        }

        if (!TryParseInventoryView(request.InventoryView, out OwnedItemInventoryView? parsedView, out IResult inventoryViewError))
        {
            return inventoryViewError;
        }

        if (!string.IsNullOrWhiteSpace(request.Medium))
        {
            string normalizedMedium = request.Medium.Trim();
            items = items.Where(item => EF.Property<string>(item, MediumTypeProperty) == normalizedMedium);
        }

        if (!string.IsNullOrWhiteSpace(request.StorageLocation))
        {
            string storageLocationPattern = $"%{request.StorageLocation.Trim()}%";
            items = items.Where(item =>
                EF.Property<string?>(item, StorageLocationProperty) != null &&
                EF.Functions.ILike(EF.Property<string>(item, StorageLocationProperty), storageLocationPattern));
        }

        if (parsedView is { } view)
        {
            items = ApplyInventoryView(items, collectionItems, view);
        }

        int total = await items.CountAsync(cancellationToken);
        OwnedItem[] page = await items
            .OrderBy(item => item.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);
        IReadOnlyList<OwnedItemResponse> responses = await OwnedItemResponseMapper.ToResponsesAsync(
            context,
            currentCollection.CollectionId,
            page,
            cancellationToken);

        return Results.Ok(new ListResponse<OwnedItemResponse>(responses, normalizedLimit, normalizedOffset, total));
    }

    private static bool ApplyStatusFilter(
        ref IQueryable<OwnedItem> items,
        string? status,
        out IResult error)
    {
        error = Results.Empty;
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (!OwnedItemMapper.TryParseOwnershipStatus(status, out OwnershipStatus normalizedStatus))
        {
            error = EndpointErrors.BadRequest("owned_item.status_invalid", "Owned item status is invalid");
            return false;
        }

        items = items.Where(item => EF.Property<OwnershipStatus>(item, StatusProperty) == normalizedStatus);
        return true;
    }

    private static bool ApplyConditionFilter(
        ref IQueryable<OwnedItem> items,
        string? condition,
        out IResult error)
    {
        error = Results.Empty;
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        if (!OwnedItemMapper.TryParseItemCondition(condition, out ItemCondition normalizedCondition))
        {
            error = EndpointErrors.BadRequest("owned_item.condition_invalid", "Owned item condition is invalid");
            return false;
        }

        items = items.Where(item => EF.Property<ItemCondition?>(item, ConditionProperty) == normalizedCondition);
        return true;
    }

    private static IQueryable<OwnedItem> ApplyInventoryView(
        IQueryable<OwnedItem> items,
        IQueryable<OwnedItem> collectionItems,
        OwnedItemInventoryView view)
    {
        if (view == OwnedItemInventoryView.NeedsDigitization)
        {
            return items.Where(item => EF.Property<OwnershipStatus>(item, StatusProperty) == OwnershipStatus.NeedsDigitization);
        }

        IQueryable<ReleaseId?> releaseIds = InventoryTargetIds<ReleaseId>(
            collectionItems,
            view,
            ReleaseTargetType,
            TargetReleaseIdProperty);
        IQueryable<TrackId?> trackIds = InventoryTargetIds<TrackId>(
            collectionItems,
            view,
            TrackTargetType,
            TargetTrackIdProperty);

        return items.Where(item =>
            (EF.Property<string>(item, TargetTypeProperty) == ReleaseTargetType &&
                releaseIds.Contains(EF.Property<ReleaseId?>(item, TargetReleaseIdProperty))) ||
            (EF.Property<string>(item, TargetTypeProperty) == TrackTargetType &&
                trackIds.Contains(EF.Property<TrackId?>(item, TargetTrackIdProperty))));
    }

    private static IQueryable<TTargetId?> InventoryTargetIds<TTargetId>(
        IQueryable<OwnedItem> collectionItems,
        OwnedItemInventoryView view,
        string targetType,
        string targetIdProperty)
        where TTargetId : struct
    {
        IQueryable<IGrouping<TTargetId?, OwnedItem>> groups = collectionItems
            .Where(item => EF.Property<string>(item, TargetTypeProperty) == targetType)
            .GroupBy(item => EF.Property<TTargetId?>(item, targetIdProperty));

        return view switch
        {
            OwnedItemInventoryView.PhysicalWithoutDigital => groups
                .Where(group =>
                    group.Any(item => EF.Property<AudioFileFormat?>(item, DigitalFileFormatProperty) == null) &&
                    !group.Any(item => EF.Property<AudioFileFormat?>(item, DigitalFileFormatProperty) != null))
                .Select(group => group.Key),
            OwnedItemInventoryView.LossyWithoutLossless => groups
                .Where(group =>
                    group.Any(item => LossyFormats.Contains(EF.Property<AudioFileFormat?>(item, DigitalFileFormatProperty))) &&
                    !group.Any(item => LosslessFormats.Contains(EF.Property<AudioFileFormat?>(item, DigitalFileFormatProperty))))
                .Select(group => group.Key),
            OwnedItemInventoryView.WantedNotOwned => groups
                .Where(group =>
                    group.Any(item => EF.Property<OwnershipStatus>(item, StatusProperty) == OwnershipStatus.Wanted) &&
                    !group.Any(item => EF.Property<OwnershipStatus>(item, StatusProperty) == OwnershipStatus.Owned))
                .Select(group => group.Key),
            OwnedItemInventoryView.NeedsDigitization => collectionItems
                .Where(_ => false)
                .Select(item => EF.Property<TTargetId?>(item, targetIdProperty)),
            _ => throw new InvalidOperationException("Owned item inventory view is not supported")
        };
    }

    private static bool TryParseInventoryView(
        string? inventoryView,
        out OwnedItemInventoryView? view,
        out IResult error)
    {
        error = Results.Empty;
        view = null;
        if (string.IsNullOrWhiteSpace(inventoryView))
        {
            return true;
        }

        view = inventoryView.Trim() switch
        {
            "physicalWithoutDigital" => OwnedItemInventoryView.PhysicalWithoutDigital,
            "lossyWithoutLossless" => OwnedItemInventoryView.LossyWithoutLossless,
            "wantedNotOwned" => OwnedItemInventoryView.WantedNotOwned,
            "needsDigitization" => OwnedItemInventoryView.NeedsDigitization,
            _ => null
        };
        if (view is not null)
        {
            return true;
        }

        error = EndpointErrors.BadRequest("owned_item.inventory_view_invalid", "Owned item inventory view is invalid");
        return false;
    }

    private enum OwnedItemInventoryView
    {
        PhysicalWithoutDigital,
        LossyWithoutLossless,
        WantedNotOwned,
        NeedsDigitization
    }
}
