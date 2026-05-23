using Cratebase.Domain.Collection;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private static async Task<PlaylistItemResponse[]> ResolveSmartResultsAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        SmartPlaylistRules rules = playlist.Rules;
        OwnedItem[] ownedItems = await LoadOwnedItemsForRulesAsync(context, playlist.CollectionId, rules, cancellationToken);
        Dictionary<ReleaseId, OwnedItem[]> releaseItems = ReleaseOwnedItemLookup(ownedItems);
        Dictionary<TrackId, OwnedItem[]> trackItems = TrackOwnedItemLookup(ownedItems);

        IQueryable<Domain.Catalog.Release> releaseQuery = context.Releases
            .AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(release => release.CollectionId == playlist.CollectionId);
        IQueryable<Domain.Catalog.Track> trackQuery = context.Tracks
            .AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(track => track.CollectionId == playlist.CollectionId);

        if (UsesOwnedItemRules(rules))
        {
            ReleaseId[] releaseIds = [.. releaseItems.Keys];
            TrackId[] trackIds = [.. trackItems.Keys];
            releaseQuery = releaseQuery.Where(release => releaseIds.Contains(release.Id));
            trackQuery = trackQuery.Where(track => trackIds.Contains(track.Id));
        }

        Domain.Catalog.Release[] releases = await releaseQuery.ToArrayAsync(cancellationToken);
        Domain.Catalog.Track[] tracks = await trackQuery.ToArrayAsync(cancellationToken);
        Domain.Catalog.Release[] trackYearReleases = UsesOwnedItemRules(rules) && UsesYearRules(rules)
            ? await context.Releases.AsNoTracking()
                .Where(release => release.CollectionId == playlist.CollectionId)
                .ToArrayAsync(cancellationToken)
            : releases;

        PlaylistItemResponse[] releaseResults =
        [
            .. releases
                .Where(release => MatchesRelease(release, ReleaseItems(releaseItems, release.Id), rules))
                .OrderBy(release => release.Summary.Title)
                .ThenBy(release => release.Id.Value)
                .Take(SmartPlaylistResultLimit)
                .Select(release => new PlaylistItemResponse(PlaylistEntry.ReleaseKind, release.Id.Value, release.Summary.Title, ReleaseYear(release)))
        ];
        int remainingTrackLimit = Math.Max(0, SmartPlaylistResultLimit - releaseResults.Length);
        PlaylistItemResponse[] trackResults =
        [
            .. tracks
                .Where(track => MatchesTrack(track, trackYearReleases, TrackItems(trackItems, track.Id), rules))
                .OrderBy(track => track.Title)
                .ThenBy(track => track.Id.Value)
                .Take(remainingTrackLimit)
                .Select(track => new PlaylistItemResponse(PlaylistEntry.TrackKind, track.Id.Value, track.Title, null))
        ];

        return [.. releaseResults, .. trackResults];
    }

    private static async Task<OwnedItem[]> LoadOwnedItemsForRulesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        SmartPlaylistRules rules,
        CancellationToken cancellationToken)
    {
        if (!UsesOwnedItemRules(rules))
        {
            return [];
        }

        OwnershipStatus[] statuses =
        [
            .. rules.OwnershipStatuses
                .Select(StatusFromCode)
                .Where(status => status.HasValue)
                .Select(status => status.GetValueOrDefault())
        ];
        if (rules.OwnershipStatuses.Count > 0 && statuses.Length == 0)
        {
            return [];
        }

        IQueryable<OwnedItem> query = context.OwnedItems.AsNoTracking()
            .Where(item => item.CollectionId == collectionId);
        if (rules.Media.Count > 0 && statuses.Length > 0)
        {
            query = query.Where(item =>
                rules.Media.Contains(EF.Property<string>(item, "_mediumType")) ||
                statuses.Contains(EF.Property<OwnershipStatus>(item, "_status")));
        }
        else if (rules.Media.Count > 0)
        {
            query = query.Where(item => rules.Media.Contains(EF.Property<string>(item, "_mediumType")));
        }
        else if (statuses.Length > 0)
        {
            query = query.Where(item => statuses.Contains(EF.Property<OwnershipStatus>(item, "_status")));
        }

        return await query.ToArrayAsync(cancellationToken);
    }

    private static Dictionary<ReleaseId, OwnedItem[]> ReleaseOwnedItemLookup(IReadOnlyList<OwnedItem> ownedItems)
    {
        return ownedItems
            .Where(item => item.Target is ReleaseOwnedItemTarget)
            .GroupBy(item => ((ReleaseOwnedItemTarget)item.Target).ReleaseId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static Dictionary<TrackId, OwnedItem[]> TrackOwnedItemLookup(IReadOnlyList<OwnedItem> ownedItems)
    {
        return ownedItems
            .Where(item => item.Target is TrackOwnedItemTarget)
            .GroupBy(item => ((TrackOwnedItemTarget)item.Target).TrackId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static bool MatchesRelease(
        Domain.Catalog.Release release,
        IReadOnlyList<OwnedItem> ownedItems,
        SmartPlaylistRules rules)
    {
        return MatchesValues(rules.Tags, release.Cataloging.Tags.Select(tag => tag.Name)) &&
            MatchesValues(rules.Genres, release.Cataloging.Genres.Select(genre => genre.Name)) &&
            MatchesValues(rules.Media, ownedItems.Select(item => item.Holding.Medium.Code)) &&
            MatchesValues(rules.OwnershipStatuses, ownedItems.Select(item => StatusCode(item.Holding.Status))) &&
            MatchesYear(ReleaseYearValue(release), rules);
    }

    private static bool MatchesTrack(
        Domain.Catalog.Track track,
        IReadOnlyList<Domain.Catalog.Release> releases,
        IReadOnlyList<OwnedItem> ownedItems,
        SmartPlaylistRules rules)
    {
        int? year = releases
            .Where(release => release.Tracklist.Any(item => item.TrackId == track.Id))
            .Select(ReleaseYearValue)
            .FirstOrDefault(value => value.HasValue);
        return MatchesValues(rules.Tags, track.Cataloging.Tags.Select(tag => tag.Name)) &&
            MatchesValues(rules.Genres, track.Cataloging.Genres.Select(genre => genre.Name)) &&
            MatchesValues(rules.Media, ownedItems.Select(item => item.Holding.Medium.Code)) &&
            MatchesValues(rules.OwnershipStatuses, ownedItems.Select(item => StatusCode(item.Holding.Status))) &&
            MatchesYear(year, rules);
    }

    private static bool UsesOwnedItemRules(SmartPlaylistRules rules)
    {
        return rules.Media.Count > 0 || rules.OwnershipStatuses.Count > 0;
    }

    private static bool UsesYearRules(SmartPlaylistRules rules)
    {
        return rules.YearFrom.HasValue || rules.YearTo.HasValue;
    }

    private static OwnedItem[] ReleaseItems(Dictionary<ReleaseId, OwnedItem[]> lookup, ReleaseId releaseId)
    {
        return lookup.TryGetValue(releaseId, out OwnedItem[]? items) ? items : [];
    }

    private static OwnedItem[] TrackItems(Dictionary<TrackId, OwnedItem[]> lookup, TrackId trackId)
    {
        return lookup.TryGetValue(trackId, out OwnedItem[]? items) ? items : [];
    }

    private static bool MatchesValues(IReadOnlyList<string> required, IEnumerable<string> values)
    {
        return required.Count == 0 || values.Any(value => required.Contains(value, StringComparer.OrdinalIgnoreCase));
    }

    private static bool MatchesYear(int? year, SmartPlaylistRules rules)
    {
        return (rules.YearFrom is not PresentOptionalValue<int> from || (year.HasValue && year.Value >= from.Value)) &&
            (rules.YearTo is not PresentOptionalValue<int> to || (year.HasValue && year.Value <= to.Value));
    }

    private static OwnershipStatus? StatusFromCode(string status)
    {
        return status switch
        {
            "owned" => OwnershipStatus.Owned,
            "wanted" => OwnershipStatus.Wanted,
            "sold" => OwnershipStatus.Sold,
            "needsDigitization" => OwnershipStatus.NeedsDigitization,
            _ => null
        };
    }
}
