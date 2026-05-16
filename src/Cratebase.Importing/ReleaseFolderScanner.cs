using Cratebase.Application.Imports;
using Cratebase.Domain.Imports;

namespace Cratebase.Importing;

public sealed class ReleaseFolderScanner
{
    public const long MaxCoverArtifactSizeBytes = 10 * 1024 * 1024;
    private readonly IAudioMetadataReader _metadataReader;

    public ReleaseFolderScanner(IAudioMetadataReader metadataReader)
    {
        _metadataReader = metadataReader;
    }

    public ReleaseFolderScanPayload Scan(
        string rootPath,
        IReadOnlyList<string> releaseTemplates,
        IReadOnlyList<string> trackTemplates,
        bool includeCoverArtifacts)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        ReleaseFolderFileScan scan = ReleaseFolderFileScanner.Scan(fullRoot);
        ReleaseFolderScanDraft[] drafts =
        [
            .. scan.Groups.Select(group => CreateDraft(fullRoot, group, releaseTemplates, trackTemplates, includeCoverArtifacts))
        ];

        return new ReleaseFolderScanPayload(fullRoot, drafts, scan.IgnoredFileCount);
    }

    private ReleaseFolderScanDraft CreateDraft(
        string rootPath,
        ReleaseFileGroup group,
        IReadOnlyList<string> releaseTemplates,
        IReadOnlyList<string> trackTemplates,
        bool includeCoverArtifacts)
    {
        ParsedReleaseFolder parsed = ReleaseFolderNameParser.Parse(group.ReleaseRoot.Name, releaseTemplates);
        AudioMetadata releaseTags = FirstReleaseTags(group);
        CoverCandidateResult cover = ReleaseCoverCandidateSelector.Select(group.ReleaseRoot);
        CoverArtifactPayload? coverArtifact = includeCoverArtifacts && cover.File is not null
            ? CreateCoverArtifact(cover.File)
            : null;
        IReadOnlyList<ImportReviewIssue> coverIssues = cover.File is not null && includeCoverArtifacts && coverArtifact is null
            ? [.. cover.Issues, new ImportReviewIssue("release_import.cover_too_large", "Selected cover image is too large to attach to the import draft")]
            : cover.Issues;

        return new ReleaseFolderScanDraft(
            group.ReleaseRoot.FullName,
            Path.GetRelativePath(rootPath, group.ReleaseRoot.FullName),
            releaseTags.AlbumTitle ?? parsed.Title ?? group.ReleaseRoot.Name,
            parsed.IsVariousArtists ? "compilation" : "unknown",
            releaseTags.CatalogNumber ?? parsed.CatalogNumber,
            null,
            releaseTags.ReleaseDate ?? parsed.ReleaseDate,
            releaseTags.Year ?? parsed.Year,
            releaseTags.AlbumArtists.Count == 0 && parsed.IsVariousArtists,
            false,
            cover.File?.FullName,
            releaseTags.AlbumArtists.Count > 0 ? releaseTags.AlbumArtists : parsed.ArtistNames,
            [],
            [],
            [],
            [.. parsed.Issues.Concat(coverIssues)],
            coverArtifact,
            [.. group.AudioFiles.Select(file => CreateTrack(group.ReleaseRoot.FullName, file, trackTemplates))]);
    }

    private AudioMetadata FirstReleaseTags(ReleaseFileGroup group)
    {
        return group.AudioFiles
            .Select(file => _metadataReader.Read(file.FullName))
            .FirstOrDefault(metadata => metadata.AlbumTitle is not null || metadata.AlbumArtists.Count > 0 || metadata.ReleaseDate is not null || metadata.Year is not null) ??
            new AudioMetadata(null, [], null, [], null, null, null, null, null);
    }

    private ReleaseFolderScanTrack CreateTrack(string releaseRoot, FileInfo file, IReadOnlyList<string> trackTemplates)
    {
        ParsedTrackFile parsed = TrackFileNameParser.Parse(file.Name, trackTemplates);
        AudioMetadata tags = _metadataReader.Read(file.FullName);

        return new ReleaseFolderScanTrack(
            file.FullName,
            Path.GetRelativePath(releaseRoot, file.FullName),
            ReleaseImportFileRules.FormatFromPath(file.FullName),
            file.Length,
            file.LastWriteTimeUtc,
            tags.Duration,
            tags.TrackNumber ?? parsed.Position,
            tags.Title ?? parsed.Title ?? Path.GetFileNameWithoutExtension(file.Name),
            tags.Artists.Count > 0 ? tags.Artists : parsed.ArtistNames,
            parsed.Issues);
    }

    private static CoverArtifactPayload? CreateCoverArtifact(FileInfo file)
    {
        if (file.Length > MaxCoverArtifactSizeBytes)
        {
            return null;
        }

        string extension = file.Extension.ToLowerInvariant();
        byte[] content = File.ReadAllBytes(file.FullName);

        return new CoverArtifactPayload(
            file.FullName,
            file.Name,
            extension,
            ReleaseImportFileRules.CoverContentType(extension),
            file.Length,
            Convert.ToBase64String(content));
    }
}
