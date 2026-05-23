using Cratebase.Domain.Collection;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    public static PlaylistType ParseType(string? type)
    {
        return (type ?? string.Empty).Trim() switch
        {
            "manual" => PlaylistType.Manual,
            "smart" => PlaylistType.Smart,
            _ => throw new DomainException("playlist.type_invalid", "Playlist type is invalid")
        };
    }

    public static string TypeCode(PlaylistType type)
    {
        return type switch
        {
            PlaylistType.Manual => "manual",
            PlaylistType.Smart => "smart",
            _ => throw new InvalidOperationException("Playlist type is not supported")
        };
    }

    public static SmartPlaylistRules ToRules(SmartPlaylistRulesRequest? request)
    {
        return request is null
            ? SmartPlaylistRules.Empty
            : SmartPlaylistRules.Create(
                request.Tags ?? [],
                request.Genres ?? [],
                request.Media ?? [],
                request.OwnershipStatuses ?? [],
                OptionalYear(request.YearFrom),
                OptionalYear(request.YearTo));
    }

    public static async Task<PlaylistEntry[]> ToEntriesAsync(
        IReadOnlyList<PlaylistEntryRequest> requests,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        var entries = new List<PlaylistEntry>(requests.Count);
        for (int index = 0; index < requests.Count; index++)
        {
            PlaylistEntryRequest request = requests[index];
            entries.Add(await ToEntryAsync(index, request, context, collectionId, cancellationToken));
        }

        return [.. entries];
    }

    public static async Task<PlaylistResponse> ToResponseAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        PlaylistItemResponse[] entries = await ResolveEntriesAsync(playlist, context, cancellationToken);
        PlaylistItemResponse[] results = playlist.Type == PlaylistType.Manual
            ? entries
            : await ResolveSmartResultsAsync(playlist, context, cancellationToken);
        SmartPlaylistRules rules = playlist.Rules;

        return new PlaylistResponse(
            playlist.Id.Value,
            playlist.Name,
            OptionalStringOrNull(playlist.Description),
            TypeCode(playlist.Type),
            new SmartPlaylistRulesResponse(
                rules.Tags,
                rules.Genres,
                rules.Media,
                rules.OwnershipStatuses,
                OptionalIntOrNull(rules.YearFrom),
                OptionalIntOrNull(rules.YearTo)),
            entries,
            results);
    }

    private static async Task<PlaylistEntry> ToEntryAsync(
        int index,
        PlaylistEntryRequest request,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return request.Kind.Trim() switch
        {
            PlaylistEntry.ReleaseKind => await RequireReleaseEntryAsync(index, request.Id, context, collectionId, cancellationToken),
            PlaylistEntry.TrackKind => await RequireTrackEntryAsync(index, request.Id, context, collectionId, cancellationToken),
            _ => throw new DomainException("playlist.entry_kind_invalid", "Playlist entry kind is invalid")
        };
    }

    private static async Task<PlaylistEntry> RequireReleaseEntryAsync(
        int index,
        Guid id,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        var releaseId = new ReleaseId(id);
        bool exists = await context.Releases.AnyAsync(release => release.CollectionId == collectionId && release.Id == releaseId, cancellationToken);
        return exists ? PlaylistEntry.ForRelease(index, releaseId) : throw new DomainException("playlist.release_not_found", "Playlist release was not found");
    }

    private static async Task<PlaylistEntry> RequireTrackEntryAsync(
        int index,
        Guid id,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        var trackId = new TrackId(id);
        bool exists = await context.Tracks.AnyAsync(track => track.CollectionId == collectionId && track.Id == trackId, cancellationToken);
        return exists ? PlaylistEntry.ForTrack(index, trackId) : throw new DomainException("playlist.track_not_found", "Playlist track was not found");
    }

    private static async Task<PlaylistItemResponse[]> ResolveEntriesAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        var items = new List<PlaylistItemResponse>();
        foreach (PlaylistEntry entry in playlist.Entries.OrderBy(entry => entry.Position))
        {
            PlaylistItemResponse? item = await ResolveEntryAsync(entry, context, playlist.CollectionId, cancellationToken);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return [.. items];
    }

    private static async Task<PlaylistItemResponse?> ResolveEntryAsync(
        PlaylistEntry entry,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (entry.Kind == PlaylistEntry.ReleaseKind && entry.ReleaseId is PresentOptionalValue<ReleaseId> release)
        {
            Domain.Catalog.Release? releaseEntity = await context.Releases.AsNoTracking().SingleOrDefaultAsync(
                item => item.CollectionId == collectionId && item.Id == release.Value,
                cancellationToken);

            return releaseEntity is null
                ? null
                : new PlaylistItemResponse(PlaylistEntry.ReleaseKind, releaseEntity.Id.Value, releaseEntity.Summary.Title, ReleaseYear(releaseEntity));
        }

        if (entry.Kind == PlaylistEntry.TrackKind && entry.TrackId is PresentOptionalValue<TrackId> trackId)
        {
            Domain.Catalog.Track? trackEntity = await context.Tracks.AsNoTracking().SingleOrDefaultAsync(
                item => item.CollectionId == collectionId && item.Id == trackId.Value,
                cancellationToken);

            return trackEntity is null
                ? null
                : new PlaylistItemResponse(PlaylistEntry.TrackKind, trackEntity.Id.Value, trackEntity.Title, null);
        }

        return null;
    }

    private static async Task<PlaylistItemResponse[]> ResolveSmartResultsAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        SmartPlaylistRules rules = playlist.Rules;
        Domain.Catalog.Release[] releases = await context.Releases
            .AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(release => release.CollectionId == playlist.CollectionId)
            .ToArrayAsync(cancellationToken);
        Domain.Catalog.Track[] tracks = await context.Tracks
            .AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(track => track.CollectionId == playlist.CollectionId)
            .ToArrayAsync(cancellationToken);
        OwnedItem[] ownedItems = await context.OwnedItems
            .AsNoTracking()
            .Where(item => item.CollectionId == playlist.CollectionId)
            .ToArrayAsync(cancellationToken);

        PlaylistItemResponse[] releaseResults =
        [
            .. releases
                .Where(release => MatchesRelease(release, ownedItems, rules))
                .OrderBy(release => release.Summary.Title)
                .ThenBy(release => release.Id.Value)
                .Select(release => new PlaylistItemResponse(PlaylistEntry.ReleaseKind, release.Id.Value, release.Summary.Title, ReleaseYear(release)))
        ];
        PlaylistItemResponse[] trackResults =
        [
            .. tracks
                .Where(track => MatchesTrack(track, releases, ownedItems, rules))
                .OrderBy(track => track.Title)
                .ThenBy(track => track.Id.Value)
                .Select(track => new PlaylistItemResponse(PlaylistEntry.TrackKind, track.Id.Value, track.Title, null))
        ];

        return [.. releaseResults, .. trackResults];
    }

    private static bool MatchesRelease(
        Domain.Catalog.Release release,
        IReadOnlyList<OwnedItem> ownedItems,
        SmartPlaylistRules rules)
    {
        OwnedItem[] releaseItems = [.. ownedItems.Where(item => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id)];
        return MatchesValues(rules.Tags, release.Cataloging.Tags.Select(tag => tag.Name)) &&
            MatchesValues(rules.Genres, release.Cataloging.Genres.Select(genre => genre.Name)) &&
            MatchesValues(rules.Media, releaseItems.Select(item => item.Holding.Medium.Code)) &&
            MatchesValues(rules.OwnershipStatuses, releaseItems.Select(item => StatusCode(item.Holding.Status))) &&
            MatchesYear(ReleaseYearValue(release), rules);
    }

    private static bool MatchesTrack(
        Domain.Catalog.Track track,
        IReadOnlyList<Domain.Catalog.Release> releases,
        IReadOnlyList<OwnedItem> ownedItems,
        SmartPlaylistRules rules)
    {
        OwnedItem[] trackItems = [.. ownedItems.Where(item => item.Target is TrackOwnedItemTarget target && target.TrackId == track.Id)];
        int? year = releases
            .Where(release => release.Tracklist.Any(item => item.TrackId == track.Id))
            .Select(ReleaseYearValue)
            .FirstOrDefault(value => value.HasValue);
        return MatchesValues(rules.Tags, track.Cataloging.Tags.Select(tag => tag.Name)) &&
            MatchesValues(rules.Genres, track.Cataloging.Genres.Select(genre => genre.Name)) &&
            MatchesValues(rules.Media, trackItems.Select(item => item.Holding.Medium.Code)) &&
            MatchesValues(rules.OwnershipStatuses, trackItems.Select(item => StatusCode(item.Holding.Status))) &&
            MatchesYear(year, rules);
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

}
