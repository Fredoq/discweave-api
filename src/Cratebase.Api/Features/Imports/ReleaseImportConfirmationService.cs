using Cratebase.Api.Features.Settings;
using Cratebase.Application.Catalog.Releases;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Imports;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportConfirmationService
{
    private const string MainArtistRole = "mainArtist";
    private readonly IReleaseCoverStorage _coverStorage;

    public ReleaseImportConfirmationService(IReleaseCoverStorage coverStorage)
    {
        _coverStorage = coverStorage;
    }

    public async Task<ReleaseImportSession?> ConfirmAsync(
        Guid sessionId,
        Guid draftId,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        ReleaseImportDraft? draft = await FindDraftForUpdateAsync(context, collectionId, sessionId, draftId, cancellationToken);
        ReleaseImportSession? session = await context.ReleaseImportSessions.SingleOrDefaultAsync(
            candidate => candidate.CollectionId == collectionId && candidate.Id == new ReleaseImportSessionId(sessionId),
            cancellationToken);
        if (session is null || draft is null)
        {
            return null;
        }

        if (draft.Status == ReleaseImportDraftStatus.Confirmed)
        {
            await transaction.CommitAsync(cancellationToken);
            return session;
        }

        if (draft.Status == ReleaseImportDraftStatus.Skipped)
        {
            throw new DomainException("release_import_draft.skipped", "Skipped release import drafts cannot be confirmed");
        }

        ReleaseImportDraftTrack[] tracks = await context.ReleaseImportDraftTracks
            .Where(track => track.CollectionId == collectionId && track.DraftId == draft.Id && !track.IsSkipped)
            .OrderBy(track => track.Position ?? 9999)
            .ThenBy(track => track.RelativePath)
            .ToArrayAsync(cancellationToken);
        if (tracks.Length == 0)
        {
            throw new DomainException("release_import.tracks_required", "Release import draft has no tracks to confirm");
        }

        Release? existingRelease = await FindExistingReleaseForSelectedTracksAsync(context, collectionId, draft, tracks, cancellationToken);
        if (existingRelease is not null)
        {
            draft.Confirm(existingRelease.Id);
            await UpdateSessionStatusAsync(context, session, cancellationToken);
            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return session;
        }

        Release release = await CreateReleaseAsync(context, collectionId, draft, tracks, cancellationToken);
        draft.Confirm(release.Id);
        await UpdateSessionStatusAsync(context, session, cancellationToken);
        _ = await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return session;
    }

    private static async Task<Release?> FindExistingReleaseForSelectedTracksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraft draft,
        ReleaseImportDraftTrack[] tracks,
        CancellationToken cancellationToken)
    {
        TrackId[] selectedTrackIds = [.. tracks.Select(track => track.SelectedTrackId).Where(id => id.HasValue).Select(id => id!.Value)];
        if (selectedTrackIds.Length != tracks.Length)
        {
            return null;
        }

        Release[] candidates = await context.Releases
            .Include(release => release.Tracklist)
            .Where(release => release.CollectionId == collectionId && release.Summary.Title == draft.Title)
            .ToArrayAsync(cancellationToken);

        return candidates.FirstOrDefault(release =>
            release.Tracklist.Count == selectedTrackIds.Length &&
            release.Tracklist
                .OrderBy(track => track.Position.Number)
                .Select(track => track.TrackId)
                .SequenceEqual(selectedTrackIds));
    }

    private static async Task<ReleaseImportDraft?> FindDraftForUpdateAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Guid sessionId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var typedSessionId = new ReleaseImportSessionId(sessionId);
        var typedDraftId = new ReleaseImportDraftId(draftId);

        return await context.ReleaseImportDrafts
            .FromSqlInterpolated($"""
                SELECT *
                FROM release_import_drafts
                WHERE collection_id = {collectionId.Value}
                  AND release_import_session_id = {typedSessionId.Value}
                  AND release_import_draft_id = {typedDraftId.Value}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Release> CreateReleaseAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraft draft,
        IReadOnlyList<ReleaseImportDraftTrack> draftTracks,
        CancellationToken cancellationToken)
    {
        string releaseType = await DictionaryValidation.RequireActiveCodeAsync(
            context,
            collectionId,
            DictionaryKind.ReleaseType,
            draft.Type,
            "release.type_invalid",
            "Release type is invalid",
            cancellationToken);
        IReadOnlyList<string> genres = await DictionaryValidation.RequireActiveCodesAsync(
            context,
            collectionId,
            DictionaryKind.Genre,
            draft.Genres,
            "release.genre_invalid",
            "Release genre is invalid",
            cancellationToken);
        var release = Release.Create(collectionId, ReleaseId.New(), draft.Title);
        ReleaseMetadata metadata = ReleaseMetadata.Empty.WithType(releaseType);

        if (draft.Year is { } year)
        {
            metadata = metadata.WithReleaseYear(year);
        }

        if (draft.ReleaseDate is { } releaseDate)
        {
            metadata = metadata.WithReleaseDate(releaseDate);
        }

        metadata = await ApplyCoverAsync(metadata, release.Id, collectionId, draft, cancellationToken);
        release.UpdateSummary(release.Summary.WithMetadata(metadata));
        release.UpdateArtistDisplay(draft.IsVariousArtists);
        release.UpdateCataloging(CatalogingMapper.Create(genres, draft.Tags));
        release.UpdateLabels(draft.NotOnLabel, await ResolveLabelsAsync(context, collectionId, draft, cancellationToken));

        _ = context.Releases.Add(release);
        await AddReleaseCreditsAsync(context, collectionId, release, draft, cancellationToken);
        await AddTracksAsync(context, collectionId, release, draft, draftTracks, cancellationToken);

        return release;
    }
}
