using DiscWeave.Domain.Imports;
using DiscWeave.Importing;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private static ReleaseFolderScanDraft CreateDraft(
        string sourceRoot,
        string releaseRootRelativePath,
        IReadOnlyList<DesktopScanFile> audioFiles,
        IReadOnlyList<DesktopScanFile> coverFiles,
        IReadOnlyList<string> releaseTemplates,
        IReadOnlyList<string> trackTemplates)
    {
        string releaseFolderName = string.IsNullOrWhiteSpace(releaseRootRelativePath)
            ? Path.GetFileName(sourceRoot)
            : LastSegment(releaseRootRelativePath);
        string sourcePath = string.IsNullOrWhiteSpace(releaseRootRelativePath)
            ? sourceRoot
            : Path.Combine(sourceRoot, releaseRootRelativePath);

        ParsedReleaseFolder parsed = ReleaseFolderNameParser.Parse(releaseFolderName, releaseTemplates);
        DesktopAudioMetadataRequest releaseTags = FirstReleaseTags(audioFiles);
        ImportDateResult releaseDate = ParseReleaseDate(releaseTags.ReleaseDate);
        CoverSelection cover = SelectCover(releaseRootRelativePath, coverFiles);

        IReadOnlyList<string> releaseArtistNames = CleanNames(releaseTags.AlbumArtists);
        bool tagsAreVariousArtists = releaseArtistNames.Count == 1 && ImportArtistNames.IsVariousArtistsName(releaseArtistNames[0]);
        bool isVariousArtists = tagsAreVariousArtists || (releaseArtistNames.Count == 0 && parsed.IsVariousArtists);
        if (tagsAreVariousArtists)
        {
            releaseArtistNames = [];
        }

        return new ReleaseFolderScanDraft(
            sourcePath,
            releaseRootRelativePath,
            TrimOrNull(releaseTags.AlbumTitle) ?? parsed.Title ?? releaseFolderName,
            isVariousArtists ? "compilation" : "unknown",
            TrimOrNull(releaseTags.CatalogNumber) ?? parsed.CatalogNumber,
            null,
            releaseDate.ReleaseDate ?? parsed.ReleaseDate,
            releaseTags.Year ?? releaseDate.Year ?? parsed.Year,
            isVariousArtists,
            false,
            cover.File?.FilePath,
            releaseArtistNames.Count > 0 ? releaseArtistNames : parsed.ArtistNames,
            [],
            [],
            [],
            [.. parsed.Issues.Concat(releaseDate.Issues).Concat(cover.Issues)],
            cover.Artifact,
            [.. audioFiles.Select(file => CreateTrack(sourcePath, file, trackTemplates))]);
    }

    private static ReleaseFolderScanTrack CreateTrack(
        string releaseRoot,
        DesktopScanFile file,
        IReadOnlyList<string> trackTemplates)
    {
        ParsedTrackFile parsed = TrackFileNameParser.Parse(Path.GetFileName(file.RelativePath), trackTemplates);
        DesktopAudioMetadataRequest? tags = file.Request.AudioMetadata;
        IReadOnlyList<string> artistNames = CleanNames(tags?.Artists);
        string? contentHash = NormalizeContentHash(file.Request.ContentHash);
        IReadOnlyList<ImportReviewIssue> issues = contentHash is null
            ? [
                .. parsed.Issues,
                new ImportReviewIssue(
                    ImportIssueCodes.ContentHashMissing,
                    "Desktop audio file is missing a SHA-256 content hash; duplicate detection will fall back to path, size, and last modified time")
            ]
            : parsed.Issues;

        return new ReleaseFolderScanTrack(
            file.FilePath,
            Path.GetRelativePath(releaseRoot, file.FilePath),
            file.AudioFormat ?? throw new InvalidOperationException("Track file requires an audio format"),
            file.Request.SizeBytes,
            file.Request.LastModifiedAt,
            contentHash,
            tags?.DurationSeconds is null ? null : TimeSpan.FromSeconds(tags.DurationSeconds.Value),
            tags?.TrackNumber ?? parsed.Position,
            TrimOrNull(tags?.Title) ?? parsed.Title ?? Path.GetFileNameWithoutExtension(file.RelativePath),
            artistNames.Count > 0 ? artistNames : parsed.ArtistNames,
            issues);
    }

    private static DesktopAudioMetadataRequest FirstReleaseTags(IReadOnlyList<DesktopScanFile> audioFiles)
    {
        return audioFiles
            .Select(file => file.Request.AudioMetadata)
            .FirstOrDefault(metadata => metadata is not null &&
                (!string.IsNullOrWhiteSpace(metadata.AlbumTitle) ||
                    CleanNames(metadata.AlbumArtists).Count > 0 ||
                    !string.IsNullOrWhiteSpace(metadata.ReleaseDate) ||
                    metadata.Year is not null ||
                    !string.IsNullOrWhiteSpace(metadata.CatalogNumber))) ??
            new DesktopAudioMetadataRequest(null, [], null, [], null, null, null, null, null);
    }
}
