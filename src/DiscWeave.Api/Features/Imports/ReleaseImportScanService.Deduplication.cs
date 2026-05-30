using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private const string DuplicateFileIssueCode = "release_import.duplicate_file";
    private const string TargetTrackIdShadowName = "_targetTrackId";

    private static async Task ApplyDuplicateTrackMatchesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        ReleaseImportSessionId sessionId,
        CancellationToken cancellationToken)
    {
        ReleaseImportDraftId[] draftIds =
        [
            .. await context.ReleaseImportDrafts
                .Where(draft => draft.CollectionId == collectionId && draft.SessionId == sessionId)
                .Select(draft => draft.Id)
                .ToArrayAsync(cancellationToken)
        ];
        ReleaseImportDraftTrack[] tracks = await context.ReleaseImportDraftTracks
            .Where(track => track.CollectionId == collectionId && draftIds.Contains(track.DraftId))
            .ToArrayAsync(cancellationToken);
        IReadOnlyDictionary<ReleaseImportDraftTrackId, TrackId> duplicateTrackIds =
            await FindDuplicateTrackIdsAsync(context, collectionId, tracks, cancellationToken);

        foreach (ReleaseImportDraftTrack track in tracks)
        {
            if (!duplicateTrackIds.TryGetValue(track.Id, out TrackId duplicateTrackId))
            {
                continue;
            }

            ImportReviewIssue[] issues =
            [
                .. track.Issues.Where(issue => issue.Code != DuplicateFileIssueCode),
                new ImportReviewIssue(DuplicateFileIssueCode, "This audio file already exists in the collection")
            ];
            track.UpdateEditableFields(new DraftTrackEditableFields(
                track.Position,
                track.Title,
                track.Duration,
                track.ArtistNames,
                track.ArtistCredits,
                track.SelectedArtistIds,
                duplicateTrackId,
                track.IsSkipped,
                issues));
        }
    }

    private static async Task<IReadOnlyDictionary<ReleaseImportDraftTrackId, TrackId>> FindDuplicateTrackIdsAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ReleaseImportDraftTrack> tracks,
        CancellationToken cancellationToken)
    {
        var matches = new Dictionary<ReleaseImportDraftTrackId, TrackId>();
        string[] contentHashes =
        [
            .. tracks
                .Select(track => NormalizeContentHash(track.ContentHash))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
        ];
        Dictionary<string, TrackId> hashMatches = await LoadHashDuplicateMatchesAsync(context, collectionId, contentHashes, cancellationToken);
        foreach (ReleaseImportDraftTrack track in tracks)
        {
            string? normalizedHash = NormalizeContentHash(track.ContentHash);
            if (normalizedHash is not null && hashMatches.TryGetValue(normalizedHash, out TrackId duplicateTrackId))
            {
                matches[track.Id] = duplicateTrackId;
            }
        }

        ReleaseImportDraftTrack[] remainingTracks = [.. tracks.Where(track => !matches.ContainsKey(track.Id))];
        Dictionary<ImportFingerprint, TrackId> fingerprintMatches = await LoadFingerprintDuplicateMatchesAsync(
            context,
            collectionId,
            remainingTracks,
            cancellationToken);
        foreach (ReleaseImportDraftTrack track in remainingTracks)
        {
            var fingerprint = new ImportFingerprint(track.FilePath, track.SizeBytes, track.LastModifiedAt);
            if (fingerprintMatches.TryGetValue(fingerprint, out TrackId duplicateTrackId))
            {
                matches[track.Id] = duplicateTrackId;
            }
        }

        return matches;
    }

    private static async Task<Dictionary<string, TrackId>> LoadHashDuplicateMatchesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        string[] contentHashes,
        CancellationToken cancellationToken)
    {
        if (contentHashes.Length == 0)
        {
            return [];
        }

        DuplicateHashMatch[] rows = await context.OwnedItems
            .Where(item =>
                item.CollectionId == collectionId &&
                EF.Property<string?>(item, "_importIdentityContentHash") != null &&
                contentHashes.Contains(EF.Property<string>(item, "_importIdentityContentHash")) &&
                EF.Property<TrackId?>(item, TargetTrackIdShadowName) != null)
            .Select(item => new DuplicateHashMatch(
                EF.Property<string>(item, "_importIdentityContentHash"),
                EF.Property<TrackId?>(item, TargetTrackIdShadowName)))
            .ToArrayAsync(cancellationToken);
        var matches = new Dictionary<string, TrackId>(StringComparer.Ordinal);
        foreach (DuplicateHashMatch row in rows)
        {
            if (row.TrackId is { } trackId)
            {
                _ = matches.TryAdd(row.ContentHash, trackId);
            }
        }

        return matches;
    }

    private static async Task<Dictionary<ImportFingerprint, TrackId>> LoadFingerprintDuplicateMatchesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ReleaseImportDraftTrack> tracks,
        CancellationToken cancellationToken)
    {
        string[] paths = [.. tracks.Select(track => track.FilePath).Distinct(StringComparer.Ordinal)];
        if (paths.Length == 0)
        {
            return [];
        }

        DuplicateFingerprintMatch[] rows = await context.OwnedItems
            .Where(item =>
                item.CollectionId == collectionId &&
                EF.Property<string?>(item, "_importIdentityPath") != null &&
                paths.Contains(EF.Property<string>(item, "_importIdentityPath")) &&
                EF.Property<TrackId?>(item, TargetTrackIdShadowName) != null)
            .Select(item => new DuplicateFingerprintMatch(
                EF.Property<string?>(item, "_importIdentityPath"),
                EF.Property<long?>(item, "_importIdentitySizeBytes"),
                EF.Property<DateTimeOffset?>(item, "_importIdentityLastModifiedAt"),
                EF.Property<TrackId?>(item, TargetTrackIdShadowName)))
            .ToArrayAsync(cancellationToken);
        var matches = new Dictionary<ImportFingerprint, TrackId>();
        foreach (DuplicateFingerprintMatch row in rows)
        {
            if (row.Path is null || row.SizeBytes is null || row.LastModifiedAt is null || row.TrackId is null)
            {
                continue;
            }

            _ = matches.TryAdd(new ImportFingerprint(row.Path, row.SizeBytes.Value, row.LastModifiedAt.Value), row.TrackId.Value);
        }

        return matches;
    }

    private readonly record struct ImportFingerprint(string Path, long SizeBytes, DateTimeOffset LastModifiedAt);

    private sealed record DuplicateHashMatch(string ContentHash, TrackId? TrackId);

    private sealed record DuplicateFingerprintMatch(string? Path, long? SizeBytes, DateTimeOffset? LastModifiedAt, TrackId? TrackId);
}
