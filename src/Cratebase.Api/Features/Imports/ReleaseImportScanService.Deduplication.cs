using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private const string DuplicateFileIssueCode = "release_import.duplicate_file";

    private static async Task ApplyDuplicateTrackMatchesAsync(
        CratebaseDbContext context,
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

        foreach (ReleaseImportDraftTrack track in tracks)
        {
            TrackId? duplicateTrackId = await FindDuplicateTrackIdAsync(context, collectionId, track, cancellationToken);
            if (duplicateTrackId is null)
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

    private static async Task<TrackId?> FindDuplicateTrackIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraftTrack track,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(track.ContentHash))
        {
            TrackId? hashMatch = await context.OwnedItems
                .Where(item =>
                    item.CollectionId == collectionId &&
                    EF.Property<string?>(item, "_importIdentityContentHash") == track.ContentHash)
                .Select(item => EF.Property<TrackId?>(item, "_targetTrackId"))
                .FirstOrDefaultAsync(cancellationToken);
            if (hashMatch is not null)
            {
                return hashMatch;
            }
        }

        return await context.OwnedItems
            .Where(item =>
                item.CollectionId == collectionId &&
                EF.Property<string?>(item, "_importIdentityPath") == track.FilePath &&
                EF.Property<long?>(item, "_importIdentitySizeBytes") == track.SizeBytes &&
                EF.Property<DateTimeOffset?>(item, "_importIdentityLastModifiedAt") == track.LastModifiedAt)
            .Select(item => EF.Property<TrackId?>(item, "_targetTrackId"))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
