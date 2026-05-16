using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Importing;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Imports;

public sealed class LocalAgentImportScanService
{
    private readonly LocalAgentImportTokenService _tokens;

    public LocalAgentImportScanService(LocalAgentImportTokenService tokens)
    {
        _tokens = tokens;
    }

    public async Task<LocalAgentImportScanResult> AcceptAsync(
        LocalAgentScanUploadRequest request,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (request.Scan is null)
        {
            throw new DomainException("release_import.scan_required", "Local agent scan payload is required");
        }

        LocalAgentImportToken token = await _tokens.UseAsync(context, request.Token, cancellationToken);
        ReleaseImportSession session = CreateSession(context, token.CollectionId, request.Scan);
        _ = await context.SaveChangesAsync(cancellationToken);

        return new LocalAgentImportScanResult(session, token.CollectionId);
    }

    private static ReleaseImportSession CreateSession(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseFolderScanPayload scan)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var session = ReleaseImportSession.Create(collectionId, ReleaseImportSessionId.New(), scan.SourceRoot, now);
        _ = context.ReleaseImportSessions.Add(session);

        foreach (ReleaseFolderScanDraft scannedDraft in scan.Drafts)
        {
            AddDraft(context, collectionId, session.Id, scannedDraft);
        }

        session.UpdateCounts(scan.Drafts.Count, scan.Drafts.Sum(draft => draft.Tracks.Count), scan.IgnoredFileCount, now);
        return session;
    }

    private static void AddDraft(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportSessionId sessionId,
        ReleaseFolderScanDraft scannedDraft)
    {
        var draft = ReleaseImportDraft.Create(
            collectionId,
            sessionId,
            ReleaseImportDraftId.New(),
            scannedDraft.SourcePath,
            scannedDraft.RelativePath);

        draft.UpdateEditableFields(new ReleaseImportDraftEditableFields(
            scannedDraft.Title,
            scannedDraft.Type,
            scannedDraft.CatalogNumber,
            scannedDraft.LabelName,
            scannedDraft.ReleaseDate,
            scannedDraft.Year,
            scannedDraft.IsVariousArtists,
            scannedDraft.NotOnLabel,
            scannedDraft.CoverPath,
            scannedDraft.ArtistNames,
            DefaultArtistCredits(scannedDraft),
            DefaultLabels(scannedDraft),
            scannedDraft.SelectedArtistIds,
            scannedDraft.Genres,
            scannedDraft.Tags,
            scannedDraft.Issues));
        draft.SetCoverArtifact(ToCoverArtifact(scannedDraft.CoverArtifact));
        _ = context.ReleaseImportDrafts.Add(draft);

        foreach (ReleaseFolderScanTrack scannedTrack in scannedDraft.Tracks)
        {
            AddTrack(context, collectionId, draft.Id, scannedTrack);
        }
    }

    private static void AddTrack(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraftId draftId,
        ReleaseFolderScanTrack scannedTrack)
    {
        var track = ReleaseImportDraftTrack.Create(
            collectionId,
            draftId,
            ReleaseImportDraftTrackId.New(),
            new DraftTrackFileInfo(
                scannedTrack.FilePath,
                scannedTrack.RelativePath,
                scannedTrack.Format,
                scannedTrack.SizeBytes,
                scannedTrack.LastModifiedAt));

        track.UpdateEditableFields(new DraftTrackEditableFields(
            scannedTrack.Position,
            scannedTrack.Title,
            scannedTrack.Duration,
            scannedTrack.ArtistNames,
            DefaultTrackArtistCredits(scannedTrack),
            [],
            null,
            false,
            scannedTrack.Issues));
        _ = context.ReleaseImportDraftTracks.Add(track);
    }

    private static ReleaseImportCoverArtifact? ToCoverArtifact(CoverArtifactPayload? artifact)
    {
        return artifact is null
            ? null
            : new ReleaseImportCoverArtifact(
                artifact.FileName,
                artifact.Extension,
                artifact.ContentType,
                artifact.SizeBytes,
                Convert.FromBase64String(artifact.ContentBase64));
    }

    private static List<ReleaseImportArtistCredit> DefaultArtistCredits(ReleaseFolderScanDraft scannedDraft)
    {
        List<ReleaseImportArtistCredit> credits = [];
        for (int index = 0; index < scannedDraft.ArtistNames.Count; index++)
        {
            Guid? selectedArtistId = index < scannedDraft.SelectedArtistIds.Count
                ? scannedDraft.SelectedArtistIds[index]
                : null;
            credits.Add(new ReleaseImportArtistCredit(selectedArtistId, scannedDraft.ArtistNames[index], "mainArtist"));
        }

        return credits;
    }

    private static List<ReleaseImportArtistCredit> DefaultTrackArtistCredits(ReleaseFolderScanTrack scannedTrack)
    {
        return [.. scannedTrack.ArtistNames.Select(name => new ReleaseImportArtistCredit(null, name, "mainArtist"))];
    }

    private static IReadOnlyList<ReleaseImportLabel> DefaultLabels(ReleaseFolderScanDraft scannedDraft)
    {
        return string.IsNullOrWhiteSpace(scannedDraft.LabelName)
            ? []
            :
            [
                new ReleaseImportLabel(
                    null,
                    scannedDraft.LabelName,
                    scannedDraft.CatalogNumber,
                    string.IsNullOrWhiteSpace(scannedDraft.CatalogNumber))
            ];
    }
}

public sealed record LocalAgentImportScanResult(ReleaseImportSession Session, CollectionId CollectionId);
