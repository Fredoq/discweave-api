using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private const int SmartPlaylistResultLimit = 200;

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
        ReleaseId[] releaseIds =
        [
            .. requests
                .Where(request => NormalizeEntryKind(request.Kind) == PlaylistEntry.ReleaseKind)
                .Select(request => new ReleaseId(request.Id))
                .Distinct()
        ];
        TrackId[] trackIds =
        [
            .. requests
                .Where(request => NormalizeEntryKind(request.Kind) == PlaylistEntry.TrackKind)
                .Select(request => new TrackId(request.Id))
                .Distinct()
        ];
        HashSet<ReleaseId> existingReleaseIds = releaseIds.Length == 0
            ? []
            : [.. await context.Releases.AsNoTracking()
                .Where(release => release.CollectionId == collectionId && releaseIds.Contains(release.Id))
                .Select(release => release.Id)
                .ToArrayAsync(cancellationToken)];
        HashSet<TrackId> existingTrackIds = trackIds.Length == 0
            ? []
            : [.. await context.Tracks.AsNoTracking()
                .Where(track => track.CollectionId == collectionId && trackIds.Contains(track.Id))
                .Select(track => track.Id)
                .ToArrayAsync(cancellationToken)];
        var entries = new List<PlaylistEntry>(requests.Count);
        for (int index = 0; index < requests.Count; index++)
        {
            PlaylistEntryRequest request = requests[index];
            entries.Add(ToEntry(index, request, existingReleaseIds, existingTrackIds));
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

    internal static async Task<PlaylistItemResponse[]> ResolveResultsAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        return playlist.Type == PlaylistType.Manual
            ? await ResolveEntriesAsync(playlist, context, cancellationToken)
            : await ResolveSmartResultsAsync(playlist, context, cancellationToken);
    }

    private static PlaylistEntry ToEntry(
        int index,
        PlaylistEntryRequest request,
        IReadOnlySet<ReleaseId> existingReleaseIds,
        IReadOnlySet<TrackId> existingTrackIds)
    {
        string kind = NormalizeEntryKind(request.Kind);
        return kind switch
        {
            PlaylistEntry.ReleaseKind => RequireReleaseEntry(index, request.Id, existingReleaseIds),
            PlaylistEntry.TrackKind => RequireTrackEntry(index, request.Id, existingTrackIds),
            _ => throw new DomainException("playlist.entry_kind_invalid", "Playlist entry kind is invalid")
        };
    }

    private static PlaylistEntry RequireReleaseEntry(
        int index,
        Guid id,
        IReadOnlySet<ReleaseId> existingReleaseIds)
    {
        var releaseId = new ReleaseId(id);
        return existingReleaseIds.Contains(releaseId)
            ? PlaylistEntry.ForRelease(index, releaseId)
            : throw new DomainException("playlist.release_not_found", "Playlist release was not found");
    }

    private static PlaylistEntry RequireTrackEntry(
        int index,
        Guid id,
        IReadOnlySet<TrackId> existingTrackIds)
    {
        var trackId = new TrackId(id);
        return existingTrackIds.Contains(trackId)
            ? PlaylistEntry.ForTrack(index, trackId)
            : throw new DomainException("playlist.track_not_found", "Playlist track was not found");
    }

    private static async Task<PlaylistItemResponse[]> ResolveEntriesAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        PlaylistEntry[] orderedEntries = [.. playlist.Entries.OrderBy(entry => entry.Position)];
        ReleaseId[] releaseIds = ReleaseIds(orderedEntries);
        TrackId[] trackIds = TrackIds(orderedEntries);
        Dictionary<ReleaseId, Domain.Catalog.Release> releases = releaseIds.Length == 0
            ? []
            : await context.Releases.AsNoTracking()
                .Where(release => release.CollectionId == playlist.CollectionId && releaseIds.Contains(release.Id))
                .ToDictionaryAsync(release => release.Id, cancellationToken);
        Dictionary<TrackId, Domain.Catalog.Track> tracks = trackIds.Length == 0
            ? []
            : await context.Tracks.AsNoTracking()
                .Where(track => track.CollectionId == playlist.CollectionId && trackIds.Contains(track.Id))
                .ToDictionaryAsync(track => track.Id, cancellationToken);
        var items = new List<PlaylistItemResponse>();
        foreach (PlaylistEntry entry in orderedEntries)
        {
            PlaylistItemResponse? item = ResolveEntry(entry, releases, tracks);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return [.. items];
    }

    private static PlaylistItemResponse? ResolveEntry(
        PlaylistEntry entry,
        Dictionary<ReleaseId, Domain.Catalog.Release> releases,
        Dictionary<TrackId, Domain.Catalog.Track> tracks)
    {
        return entry.Kind switch
        {
            PlaylistEntry.ReleaseKind when
                entry.ReleaseId is PresentOptionalValue<ReleaseId> release &&
                releases.TryGetValue(release.Value, out Domain.Catalog.Release? releaseEntity) =>
                new PlaylistItemResponse(PlaylistEntry.ReleaseKind, releaseEntity.Id.Value, releaseEntity.Summary.Title, ReleaseYear(releaseEntity)),
            PlaylistEntry.TrackKind when
                entry.TrackId is PresentOptionalValue<TrackId> trackId &&
                tracks.TryGetValue(trackId.Value, out Domain.Catalog.Track? trackEntity) =>
                new PlaylistItemResponse(PlaylistEntry.TrackKind, trackEntity.Id.Value, trackEntity.Title, null),
            _ => null
        };
    }
}
