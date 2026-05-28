using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Cratebase.Api.Features.OwnedItems;

internal static class OwnedItemResponseMapper
{
    private const string TargetReleaseIdProperty = "_targetReleaseId";
    private const string TargetTrackIdProperty = "_targetTrackId";
    private const string TargetTypeProperty = "_targetType";
    private const string ReleaseTargetType = "release";
    private const string TrackTargetType = "track";

    public static async Task<OwnedItemResponse> ToResponseAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        OwnedItem item,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OwnedItemResponse> responses = await ToResponsesAsync(context, collectionId, [item], cancellationToken);
        return responses[0];
    }

    public static async Task<IReadOnlyList<OwnedItemResponse>> ToResponsesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<OwnedItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        ReleaseId[] releaseIds = [.. items.Select(ReleaseTargetId).Where(id => id.HasValue).Select(id => id!.Value).Distinct()];
        TrackId[] trackIds = [.. items.Select(TrackTargetId).Where(id => id.HasValue).Select(id => id!.Value).Distinct()];
        Dictionary<ReleaseId, Release> releasesById = await LoadReleasesByIdAsync(context, collectionId, releaseIds, cancellationToken);
        Dictionary<TrackId, Track> tracksById = await LoadTracksByIdAsync(context, collectionId, trackIds, cancellationToken);
        Dictionary<TrackId, Release> parentReleasesByTrackId = await LoadParentReleasesByTrackIdAsync(context, collectionId, trackIds, cancellationToken);
        OwnedItem[] targetOwnedItems = await LoadTargetOwnedItemsAsync(context, collectionId, releaseIds, trackIds, cancellationToken);
        Dictionary<ReleaseId, OwnedItem[]> ownedItemsByReleaseId = BuildOwnedItemsByReleaseId(targetOwnedItems);
        Dictionary<TrackId, OwnedItem[]> ownedItemsByTrackId = BuildOwnedItemsByTrackId(targetOwnedItems);

        return
        [
            .. items.Select(item =>
            {
                OwnedItemTargetResponse target = ToTargetResponse(item, releasesById, tracksById, parentReleasesByTrackId);
                IReadOnlyList<string> inventorySignals = CollectorSignals(TargetOwnedItems(item, ownedItemsByReleaseId, ownedItemsByTrackId));
                return OwnedItemMapper.ToResponse(item, target, inventorySignals);
            })
        ];
    }

    private static async Task<Dictionary<ReleaseId, Release>> LoadReleasesByIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseId[] releaseIds,
        CancellationToken cancellationToken)
    {
        return releaseIds.Length == 0
            ? []
            : await context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId && releaseIds.Contains(release.Id))
            .ToDictionaryAsync(release => release.Id, cancellationToken);
    }

    private static async Task<Dictionary<TrackId, Track>> LoadTracksByIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        TrackId[] trackIds,
        CancellationToken cancellationToken)
    {
        return trackIds.Length == 0
            ? []
            : await context.Tracks.AsNoTracking()
            .Where(track => track.CollectionId == collectionId && trackIds.Contains(track.Id))
            .ToDictionaryAsync(track => track.Id, cancellationToken);
    }

    private static async Task<Dictionary<TrackId, Release>> LoadParentReleasesByTrackIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        TrackId[] trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Length == 0)
        {
            return [];
        }

        Release[] releases = await context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId && release.Tracklist.Any(track => trackIds.Contains(track.TrackId)))
            .ToArrayAsync(cancellationToken);

        return releases
            .SelectMany(release => release.Tracklist
                .Where(track => trackIds.Contains(track.TrackId))
                .Select(track => new { track.TrackId, Release = release }))
            .GroupBy(row => row.TrackId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(row => row.Release.Summary.Title, StringComparer.Ordinal)
                    .ThenBy(row => row.Release.Id.Value)
                    .First()
                    .Release);
    }

    private static async Task<OwnedItem[]> LoadTargetOwnedItemsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseId[] releaseIds,
        TrackId[] trackIds,
        CancellationToken cancellationToken)
    {
        List<OwnedItem> ownedItems = [];
        if (releaseIds.Length > 0)
        {
            ownedItems.AddRange(await context.OwnedItems.AsNoTracking()
            .Where(item =>
                item.CollectionId == collectionId &&
                EF.Property<string>(item, TargetTypeProperty) == ReleaseTargetType)
            .Where(HasAnyTargetReleaseId(releaseIds))
            .ToArrayAsync(cancellationToken));
        }

        if (trackIds.Length > 0)
        {
            ownedItems.AddRange(await context.OwnedItems.AsNoTracking()
            .Where(item =>
                item.CollectionId == collectionId &&
                EF.Property<string>(item, TargetTypeProperty) == TrackTargetType)
            .Where(HasAnyTargetTrackId(trackIds))
            .ToArrayAsync(cancellationToken));
        }

        return [.. ownedItems];
    }

    private static Expression<Func<OwnedItem, bool>> HasAnyTargetReleaseId(ReleaseId[] releaseIds)
    {
        Expression<Func<OwnedItem, ReleaseId?>> targetReleaseId = item => EF.Property<ReleaseId?>(item, TargetReleaseIdProperty);
        Expression? body = null;

        foreach (ReleaseId releaseId in releaseIds)
        {
            BinaryExpression targetMatches = Expression.Equal(targetReleaseId.Body, Expression.Constant((ReleaseId?)releaseId, typeof(ReleaseId?)));
            body = body is null ? targetMatches : Expression.OrElse(body, targetMatches);
        }

        return Expression.Lambda<Func<OwnedItem, bool>>(body ?? Expression.Constant(false), targetReleaseId.Parameters);
    }

    private static Expression<Func<OwnedItem, bool>> HasAnyTargetTrackId(TrackId[] trackIds)
    {
        Expression<Func<OwnedItem, TrackId?>> targetTrackId = item => EF.Property<TrackId?>(item, TargetTrackIdProperty);
        Expression? body = null;

        foreach (TrackId trackId in trackIds)
        {
            BinaryExpression targetMatches = Expression.Equal(targetTrackId.Body, Expression.Constant((TrackId?)trackId, typeof(TrackId?)));
            body = body is null ? targetMatches : Expression.OrElse(body, targetMatches);
        }

        return Expression.Lambda<Func<OwnedItem, bool>>(body ?? Expression.Constant(false), targetTrackId.Parameters);
    }

    private static OwnedItemTargetResponse ToTargetResponse(
        OwnedItem item,
        IReadOnlyDictionary<ReleaseId, Release> releasesById,
        IReadOnlyDictionary<TrackId, Track> tracksById,
        IReadOnlyDictionary<TrackId, Release> parentReleasesByTrackId)
    {
        return item.Target switch
        {
            ReleaseOwnedItemTarget target => ToReleaseTargetResponse(target.ReleaseId, releasesById),
            TrackOwnedItemTarget target => ToTrackTargetResponse(target.TrackId, tracksById, parentReleasesByTrackId),
            _ => throw new InvalidOperationException("Owned item target is not supported")
        };
    }

    private static OwnedItemTargetResponse ToReleaseTargetResponse(
        ReleaseId releaseId,
        IReadOnlyDictionary<ReleaseId, Release> releasesById)
    {
        string title = releasesById.TryGetValue(releaseId, out Release? release) ? release.Summary.Title : "Unknown release";

        return new OwnedItemTargetResponse(
            ReleaseTargetType,
            releaseId.Value,
            title,
            "release",
            releaseId.Value,
            title);
    }

    private static OwnedItemTargetResponse ToTrackTargetResponse(
        TrackId trackId,
        IReadOnlyDictionary<TrackId, Track> tracksById,
        IReadOnlyDictionary<TrackId, Release> parentReleasesByTrackId)
    {
        string title = tracksById.TryGetValue(trackId, out Track? track) ? track.Title : "Unknown track";
        _ = parentReleasesByTrackId.TryGetValue(trackId, out Release? release);

        return new OwnedItemTargetResponse(
            TrackTargetType,
            trackId.Value,
            title,
            release?.Summary.Title ?? "track",
            release?.Id.Value,
            release?.Summary.Title);
    }

    private static OwnedItem[] TargetOwnedItems(
        OwnedItem item,
        Dictionary<ReleaseId, OwnedItem[]> ownedItemsByReleaseId,
        Dictionary<TrackId, OwnedItem[]> ownedItemsByTrackId)
    {
        return item.Target switch
        {
            ReleaseOwnedItemTarget target when ownedItemsByReleaseId.TryGetValue(target.ReleaseId, out OwnedItem[]? items) => items,
            TrackOwnedItemTarget target when ownedItemsByTrackId.TryGetValue(target.TrackId, out OwnedItem[]? items) => items,
            _ => [item]
        };
    }

    private static IReadOnlyList<string> CollectorSignals(IReadOnlyList<OwnedItem> items)
    {
        bool hasDigital = items.Any(item => item.Holding.Medium is DigitalFile);
        bool hasPhysical = items.Any(item => item.Holding.Medium is not DigitalFile);
        bool hasLossless = items.Any(item => item.Holding.Medium is DigitalFile digital && IsLossless(digital.Format));
        bool hasLossy = items.Any(item => item.Holding.Medium is DigitalFile digital && !IsLossless(digital.Format));
        List<string> signals = [.. items.Select(item => item.Holding.Medium.Code), .. items.Select(item => OwnedItemMapper.ToOwnershipStatusCode(item.Holding.Status))];
        if (hasPhysical && !hasDigital)
        {
            signals.Add("physicalWithoutDigital");
        }

        if (hasLossy && !hasLossless)
        {
            signals.Add("lossyWithoutLossless");
        }

        if (items.Any(item => item.Holding.Status == OwnershipStatus.Wanted) && !items.Any(item => item.Holding.Status == OwnershipStatus.Owned))
        {
            signals.Add("wantedNotOwned");
        }

        return [.. signals.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool IsLossless(AudioFileFormat format)
    {
        return format is AudioFileFormat.Flac or AudioFileFormat.Wav or AudioFileFormat.Aiff or AudioFileFormat.Alac;
    }

    private static ReleaseId? ReleaseTargetId(OwnedItem item)
    {
        return item.Target is ReleaseOwnedItemTarget target ? target.ReleaseId : null;
    }

    private static TrackId? TrackTargetId(OwnedItem item)
    {
        return item.Target is TrackOwnedItemTarget target ? target.TrackId : null;
    }

    private static Dictionary<ReleaseId, OwnedItem[]> BuildOwnedItemsByReleaseId(IReadOnlyList<OwnedItem> ownedItems)
    {
        return ownedItems
            .Where(item => item.Target is ReleaseOwnedItemTarget)
            .GroupBy(item => ((ReleaseOwnedItemTarget)item.Target).ReleaseId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static Dictionary<TrackId, OwnedItem[]> BuildOwnedItemsByTrackId(IReadOnlyList<OwnedItem> ownedItems)
    {
        return ownedItems
            .Where(item => item.Target is TrackOwnedItemTarget)
            .GroupBy(item => ((TrackOwnedItemTarget)item.Target).TrackId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }
}
