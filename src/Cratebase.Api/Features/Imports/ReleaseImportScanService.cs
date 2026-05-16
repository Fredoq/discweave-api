using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Importing;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportScanService
{
    public async Task<ReleaseImportScanResult> AcceptDesktopAsync(
        DesktopFolderScanRequest request,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new DomainException("release_import.scan_required", "Desktop scan payload is required");
        }

        if (string.IsNullOrWhiteSpace(request.SourceRoot))
        {
            throw new DomainException("release_import.source_root_required", "Desktop scan source root is required");
        }

        IReadOnlyList<string> releaseTemplates = await ImportPatternDefaults.ActiveTemplatesAsync(
            context,
            collectionId,
            ImportPatternKind.ReleaseFolder,
            cancellationToken);
        IReadOnlyList<string> trackTemplates = await ImportPatternDefaults.ActiveTemplatesAsync(
            context,
            collectionId,
            ImportPatternKind.TrackFile,
            cancellationToken);

        ReleaseFolderScanPayload scan = BuildScan(request, releaseTemplates, trackTemplates);
        ReleaseImportSession session = CreateSession(context, collectionId, scan);
        _ = await context.SaveChangesAsync(cancellationToken);

        return new ReleaseImportScanResult(session, collectionId);
    }

    private static ReleaseFolderScanPayload BuildScan(
        DesktopFolderScanRequest request,
        IReadOnlyList<string> releaseTemplates,
        IReadOnlyList<string> trackTemplates)
    {
        List<DesktopScanFile> audioFiles = [];
        List<DesktopScanFile> coverFiles = [];
        int ignoredFileCount = Math.Max(0, request.IgnoredFileCount);

        foreach (DesktopFolderScanFileRequest file in request.Files ?? [])
        {
            string relativePath = NormalizeRelativePath(file.RelativePath);
            if (string.IsNullOrWhiteSpace(relativePath) || IsHiddenPath(relativePath))
            {
                ignoredFileCount++;
                continue;
            }

            if (TryGetAudioFormat(file, out AudioFileFormat format))
            {
                audioFiles.Add(new DesktopScanFile(file, relativePath, RequiredFilePath(request.SourceRoot, file.FilePath, relativePath), format));
                continue;
            }

            if (IsSupportedCover(file))
            {
                coverFiles.Add(new DesktopScanFile(file, relativePath, RequiredFilePath(request.SourceRoot, file.FilePath, relativePath), null));
                continue;
            }

            ignoredFileCount++;
        }

        string sourceRoot = Path.TrimEndingDirectorySeparator(request.SourceRoot.Trim());
        ReleaseFolderScanDraft[] drafts =
        [
            .. audioFiles
                .GroupBy(file => ReleaseRootFor(file.RelativePath, audioFiles), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => CreateDraft(
                    sourceRoot,
                    group.Key,
                    [.. group.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)],
                    coverFiles,
                    releaseTemplates,
                    trackTemplates))
        ];

        return new ReleaseFolderScanPayload(sourceRoot, drafts, ignoredFileCount);
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

}

public sealed record ReleaseImportScanResult(ReleaseImportSession Session, CollectionId CollectionId);
